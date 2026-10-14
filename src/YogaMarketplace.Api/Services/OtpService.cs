using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public record OtpRequestResult(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);

/// <summary>
/// Sign-in codes. Requesting a code answers the same way for every phone (new, registered, blocked or locked),
/// so the answer never tells whether a number has an account; only a phone that can sign in actually gets a code.
/// </summary>
public class OtpService
{
    public const string LockedMessage = "Too many attempts. Try again later.";

    private readonly YogaDbContext _db;
    private readonly IOtpSender _sender;
    private readonly OtpOptions _options;
    private readonly OtpDemoMode _demo;
    private readonly ConsentService _consent;
    private readonly IPlatformSettingsService _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        YogaDbContext db,
        IOtpSender sender,
        IOptions<OtpOptions> options,
        ConsentService consent,
        IPlatformSettingsService settings,
        TimeProvider time,
        ILogger<OtpService> logger)
    {
        _db = db;
        _sender = sender;
        _consent = consent;
        _settings = settings;
        _time = time;
        _logger = logger;
        _options = options.Value;
        _demo = OtpDemoMode.From(_options);
        if (string.IsNullOrWhiteSpace(_options.Pepper) || _options.Pepper.Length < 8)
            throw new InvalidOperationException("Otp:Pepper must be at least 8 characters.");
    }

    public async Task<OtpRequestResult> RequestAsync(RequestOtpRequest request, CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryNormalize(request.Phone, out var phone, out var phoneError))
            throw new DomainException(phoneError!);

        string? name = null;
        Gender? gender = null;
        if (request.IsNewUser)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 2 || request.Name.Trim().Length > 80)
                throw new DomainException("Name is required for a new account.");
            if (!Enum.TryParse<Gender>(request.Gender, true, out var parsed))
                throw new DomainException("Gender is required for a new account. Use Female, Male, or Other.");
            if (!request.AcceptedPolicies)
                throw new DomainException("Accept the Terms and Privacy Policy to create an account.");
            name = request.Name.Trim();
            gender = parsed;
        }

        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Phone == phone, cancellationToken);
        // A registered phone on the new-account path gets a normal sign-in code; verify ignores the signup details.
        var deliver = user is null ? request.IsNewUser : !user.IsBlocked;
        var isNewUser = request.IsNewUser && user is null;
        return await IssueAsync(phone, deliver, isNewUser, name, gender, isNewUser, UserRole.Customer, cancellationToken);
    }

    public async Task<OtpRequestResult> ResendAsync(string? phoneRaw, CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryNormalize(phoneRaw, out var phone, out var phoneError))
            throw new DomainException(phoneError!);

        var previous = await LatestChallengeAsync(phone, openOnly: false, cancellationToken);

        if (previous is null)
            throw new DomainException("Request a code before resending.");

        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Phone == phone, cancellationToken);
        var deliver = user is null ? previous.IsNewUser : !user.IsBlocked;
        return await IssueAsync(
            phone,
            deliver,
            previous.IsNewUser && user is null,
            previous.PendingName,
            previous.PendingGender,
            previous.ConsentAccepted,
            previous.IntendedRole,
            cancellationToken);
    }

    public async Task<User> VerifyAsync(VerifyOtpRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryNormalize(request.Phone, out var phone, out var phoneError))
            throw new DomainException(phoneError!);
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new DomainException("Enter the code we sent.");

        var now = _time.GetUtcNow();
        if (await IsLockedAsync(phone, now, cancellationToken))
            throw new DomainException(LockedMessage, StatusCodes.Status429TooManyRequests);

        var challenge = await LatestChallengeAsync(phone, openOnly: true, cancellationToken);

        if (challenge is null)
            throw new DomainException("Request a code first.");
        if (challenge.ExpiresAt < now)
            throw new DomainException("That code has expired. Request a new one.");
        if (challenge.AttemptCount >= _options.MaxAttempts)
            throw new DomainException("Too many attempts. Request a new code.");

        var hash = OtpHasher.Hash(request.Code.Trim(), _options.Pepper);
        if (!OtpHasher.Matches(hash, challenge.CodeHash))
        {
            challenge.AttemptCount++;
            var limits = (await _settings.GetAsync(cancellationToken)).OtpLimits;
            var wrong = (await RecentChallengesAsync(phone, now, limits, cancellationToken)).Sum(c => c.AttemptCount);
            if (wrong >= limits.MaxWrongCodes)
            {
                await LockAsync(phone, now, limits, "too many wrong codes", cancellationToken);
                throw new DomainException(LockedMessage, StatusCodes.Status429TooManyRequests);
            }
            await _db.SaveChangesAsync(cancellationToken);
            throw new DomainException(challenge.AttemptCount >= _options.MaxAttempts
                ? "Too many attempts. Request a new code."
                : "That code is incorrect.");
        }

        challenge.ConsumedAt = now;
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Phone == phone, cancellationToken);
        // Checked only after a correct code, so verify can't be used to find out that a phone is blocked.
        UserBlocking.EnsureCanSignIn(user);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(challenge.PendingName) || challenge.PendingGender is null || !challenge.ConsentAccepted)
                throw new DomainException("This code is missing signup details. Start again.");

            user = new User
            {
                Id = Guid.NewGuid(),
                Phone = phone,
                Name = challenge.PendingName,
                Gender = challenge.PendingGender,
                Role = challenge.IntendedRole,
                CreatedAt = now
            };
            _db.Users.Add(user);
            // ponytail: records the version current at verify; a bump while the code is open counts as accepted. Store the version on the challenge if that matters.
            await _consent.AddAsync(user.Id, ipAddress, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return user;
    }

    /// <summary>
    /// Stores a challenge and sends its code only when <paramref name="deliver"/> is true. Otherwise the code is random
    /// and never sent, so verify answers "incorrect" just as it would for a real phone.
    /// </summary>
    private async Task<OtpRequestResult> IssueAsync(
        string phone,
        bool deliver,
        bool isNewUser,
        string? name,
        Gender? gender,
        bool consentAccepted,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.ExpiryMinutes <= 0 ? 5 : _options.ExpiryMinutes);
        var silent = new OtpRequestResult(Guid.NewGuid(), expiresAt, null);
        if (await IsLockedAsync(phone, now, cancellationToken))
            return silent;

        var limits = (await _settings.GetAsync(cancellationToken)).OtpLimits;
        var recent = await RecentChallengesAsync(phone, now, limits, cancellationToken);
        if (recent.Count >= limits.MaxSends)
        {
            await LockAsync(phone, now, limits, "too many codes requested", cancellationToken);
            return silent;
        }

        var open = await _db.OtpChallenges
            .Where(c => c.Phone == phone && c.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var challenge in open)
            challenge.ConsumedAt = now;

        var code = deliver ? NewCode(phone) : RandomCode();
        var created = new OtpChallenge
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            CodeHash = OtpHasher.Hash(code, _options.Pepper),
            ExpiresAt = expiresAt,
            IsNewUser = isNewUser,
            PendingName = name,
            PendingGender = gender,
            ConsentAccepted = consentAccepted,
            IntendedRole = role,
            CreatedAt = now
        };
        _db.OtpChallenges.Add(created);
        await _db.SaveChangesAsync(cancellationToken);
        if (!deliver)
            return silent with { ChallengeId = created.Id };

        await _sender.SendAsync(phone, code, cancellationToken);
        return new OtpRequestResult(created.Id, created.ExpiresAt, _options.ExposeCode ? code : null);
    }

    private async Task<bool> IsLockedAsync(string phone, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await _db.OtpPhoneLocks.AsNoTracking().SingleOrDefaultAsync(l => l.Phone == phone, cancellationToken);
        return row is not null && row.LockedUntil > now;
    }

    // ponytail: two requests locking the same phone at once can collide on the key; one of them fails with a 500. Upsert if that shows up.
    private async Task LockAsync(string phone, DateTimeOffset now, OtpLimitTerms limits, string reason, CancellationToken cancellationToken)
    {
        var until = now.AddMinutes(limits.LockMinutes);
        var row = await _db.OtpPhoneLocks.SingleOrDefaultAsync(l => l.Phone == phone, cancellationToken);
        if (row is null)
            _db.OtpPhoneLocks.Add(new OtpPhoneLock { Phone = phone, LockedUntil = until });
        else
            row.LockedUntil = until;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogWarning("Locked sign-in codes for {Phone} until {LockedUntil}: {Reason}.", LogMask.Phone(phone), until, reason);
    }

    /// <summary>
    /// Challenges from the last lock period. Counting over the lock length means a phone starts clean when its lock ends.
    /// </summary>
    private async Task<List<OtpChallenge>> RecentChallengesAsync(
        string phone, DateTimeOffset now, OtpLimitTerms limits, CancellationToken cancellationToken)
    {
        var since = now.AddMinutes(-limits.LockMinutes);
        var challenges = await _db.OtpChallenges.Where(c => c.Phone == phone).ToListAsync(cancellationToken);
        return challenges.Where(c => c.CreatedAt > since).ToList();
    }

    private async Task<OtpChallenge?> LatestChallengeAsync(string phone, bool openOnly, CancellationToken cancellationToken)
    {
        var query = _db.OtpChallenges.Where(c => c.Phone == phone);
        if (openOnly)
            query = query.Where(c => c.ConsumedAt == null);

        var challenges = await query.ToListAsync(cancellationToken);
        return challenges.MaxBy(c => c.CreatedAt);
    }

    private string NewCode(string phone)
    {
        if (_demo.CodeFor(phone) is string demoCode)
            return demoCode;
        if (_options.UseFixedCode && !string.IsNullOrWhiteSpace(_options.FixedCode))
            return _options.FixedCode.Trim();
        return RandomCode();
    }

    private static string RandomCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
