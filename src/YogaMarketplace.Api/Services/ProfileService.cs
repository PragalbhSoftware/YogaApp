using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public class ProfileService
{
    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;

    public ProfileService(YogaDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CustomerProfileResponse> GetMineAsync(CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == _current.UserId, cancellationToken)
            ?? throw new DomainException("Sign in required.", 401);
        return ToResponse(user);
    }

    public async Task<CustomerProfileResponse> UpdateAccountAsync(
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == _current.UserId, cancellationToken)
            ?? throw new DomainException("Sign in required.", 401);

        var name = (request.Name ?? "").Trim();
        if (name.Length is < 2 or > 80)
            throw new DomainException("Name must be 2 to 80 characters.");
        if (!Enum.TryParse<Gender>(request.Gender, true, out var gender))
            throw new DomainException("Gender is required. Use Female, Male, or Other.");

        user.Name = name;
        user.Gender = gender;
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<CustomerProfileResponse> UpdateVisitAddressAsync(
        UpdateVisitAddressRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == _current.UserId, cancellationToken)
            ?? throw new DomainException("Sign in required.", 401);
        if (user.Role != UserRole.Customer)
            throw new DomainException("Only customers can save a visit address.", 403);

        VisitAddressRules.Set(user, request.Line1, request.Area, request.City, request.Pin, request.Landmark);
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    private static CustomerProfileResponse ToResponse(User user) =>
        new(
            user.Id,
            user.Name,
            user.Phone,
            user.Gender?.ToString(),
            user.Role.ToString(),
            VisitAddressRules.IsComplete(user)
                ? new VisitAddressResponse(
                    user.HomeLine1!.Trim(),
                    user.HomeArea!.Trim(),
                    user.HomeCity!.Trim(),
                    user.HomePin!.Trim(),
                    user.HomeLandmark!.Trim(),
                    VisitAddressRules.Compose(user.HomeLine1, user.HomeArea, user.HomeCity, user.HomePin))
                : null);
}
