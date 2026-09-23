using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public record OtpRequestResult(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);

public class OtpService
{
    private readonly YogaDbContext _db;
    private readonly IOtpSender _sender;
    private readonly OtpOptions _options;

    public OtpService(YogaDbContext db, IOtpSender sender, IOptions<OtpOptions> options)
    {
        _db = db;
        _sender = sender;
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.Pepper) || _options.Pepper.Length < 8)
            throw new InvalidOperationException("Otp:Pepper must be at least 8 characters.");
    }

    public async Task<OtpRequestResult> RequestAsync(RequestOtpRequest request, CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryNormalize(request.Phone, out var phone, out var phoneError))
            throw new DomainException(phoneError!);

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Phone == phone, cancellationToken);
        string? name = null;
        Gender? gender = null;

        if (request.IsNewUser)
        {
            if (user is not null)
                throw new DomainException("An account with this phone already exists. Sign in with your phone.", 409);
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 2 || request.Name.Trim().Length > 80)
                throw new DomainException("Name is required for a new account.");
            if (!Enum.TryParse<Gender>(request.Gender, true, out var parsed))
                throw new DomainException("Gender is required for a new account. Use Female, Male, or Other.");
            name = request.Name.Trim();
            gender = parsed;
        }
        else if (user is null)
        {
            throw new DomainException("No account for this phone. Create an account first.", 404);
        }

        return await IssueAsync(phone, request.IsNewUser, name, gender, UserRole.Customer, cancellationToken);
    }

    public async Task<OtpRequestResult> ResendAsync(string? phoneRaw, CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryNormalize(phoneRaw, out var phone, out var phoneError))
            throw new DomainException(phoneError!);

        var previous = await LatestChallengeAsync(phone, openOnly: false, cancellationToken);

        if (previous is null)
            throw new DomainException("Request a code before resending.");

        var userExists = await _db.Users.AnyAsync(u => u.Phone == phone, cancellationToken);
        return await IssueAsync(
            phone,
            previous.IsNewUser && !userExists,
            previous.PendingName,
            previous.PendingGender,
            previous.IntendedRole,
            cancellationToken);
    }

    public async Task<User> VerifyAsync(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryNormalize(request.Phone, out var phone, out var phoneError))
            throw new DomainException(phoneError!);
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new DomainException("Enter the code we sent.");

        var challenge = await LatestChallengeAsync(phone, openOnly: true, cancellationToken);

        if (challenge is null)
            throw new DomainException("Request a code first.");
        if (challenge.ExpiresAt < DateTimeOffset.UtcNow)
            throw new DomainException("That code has expired. Request a new one.");
        if (challenge.AttemptCount >= _options.MaxAttempts)
            throw new DomainException("Too many attempts. Request a new code.");

        var hash = OtpHasher.Hash(request.Code.Trim(), _options.Pepper);
        if (!OtpHasher.Matches(hash, challenge.CodeHash))
        {
            challenge.AttemptCount++;
            await _db.SaveChangesAsync(cancellationToken);
            throw new DomainException(challenge.AttemptCount >= _options.MaxAttempts
                ? "Too many attempts. Request a new code."
                : "That code is incorrect.");
        }

        challenge.ConsumedAt = DateTimeOffset.UtcNow;
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Phone == phone, cancellationToken);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(challenge.PendingName) || challenge.PendingGender is null)
                throw new DomainException("This code is missing signup details. Start again.");

            user = new User
            {
                Id = Guid.NewGuid(),
                Phone = phone,
                Name = challenge.PendingName,
                Gender = challenge.PendingGender,
                Role = challenge.IntendedRole,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.Users.Add(user);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task<OtpRequestResult> IssueAsync(
        string phone,
        bool isNewUser,
        string? name,
        Gender? gender,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var open = await _db.OtpChallenges
            .Where(c => c.Phone == phone && c.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var challenge in open)
            challenge.ConsumedAt = now;

        var code = NewCode();
        var created = new OtpChallenge
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            CodeHash = OtpHasher.Hash(code, _options.Pepper),
            ExpiresAt = now.AddMinutes(_options.ExpiryMinutes <= 0 ? 5 : _options.ExpiryMinutes),
            IsNewUser = isNewUser,
            PendingName = name,
            PendingGender = gender,
            IntendedRole = role,
            CreatedAt = now
        };
        _db.OtpChallenges.Add(created);
        await _db.SaveChangesAsync(cancellationToken);
        await _sender.SendAsync(phone, code, cancellationToken);

        return new OtpRequestResult(created.Id, created.ExpiresAt, _options.ExposeCode ? code : null);
    }

    private async Task<OtpChallenge?> LatestChallengeAsync(string phone, bool openOnly, CancellationToken cancellationToken)
    {
        var query = _db.OtpChallenges.Where(c => c.Phone == phone);
        if (openOnly)
            query = query.Where(c => c.ConsumedAt == null);

        var challenges = await query.ToListAsync(cancellationToken);
        return challenges.MaxBy(c => c.CreatedAt);
    }

    private string NewCode()
    {
        if (_options.UseFixedCode && !string.IsNullOrWhiteSpace(_options.FixedCode))
            return _options.FixedCode.Trim();
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }
}
