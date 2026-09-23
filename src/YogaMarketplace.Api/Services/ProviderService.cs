using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public class ProviderService
{
    private static readonly BookingStatus[] Occupying =
        Enum.GetValues<BookingStatus>().Where(BookingRules.OccupiesSlot).ToArray();

    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly JwtTokenService _tokens;

    public ProviderService(YogaDbContext db, ICurrentUser current, JwtTokenService tokens)
    {
        _db = db;
        _current = current;
        _tokens = tokens;
    }

    public async Task<IReadOnlyList<ProviderSummary>> BrowseAsync(
        string? area,
        string? mode,
        string? category,
        CancellationToken cancellationToken)
    {
        var parsedMode = ParseOptionalMode(mode);
        var query = VerifiedQuery();

        if (!string.IsNullOrWhiteSpace(area))
        {
            var name = area.Trim().ToLower();
            query = query.Where(p => p.Area!.City == "Mumbai" && p.Area.Name.ToLower() == name);
        }

        if (parsedMode == SessionMode.Home)
            query = query.Where(p => p.OffersHome);
        else if (parsedMode == SessionMode.Studio)
            query = query.Where(p => p.OffersStudio);
        else if (parsedMode == SessionMode.Online)
            query = query.Where(p => p.OffersOnline);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var slug = category.Trim().ToLower();
            query = query.Where(p => p.Services.Any(s => s.IsActive && s.Category!.Slug == slug));
        }

        var providers = await query.OrderBy(p => p.DisplayName).Take(100).ToListAsync(cancellationToken);
        var ratings = await LoadRatingsAsync(providers.Select(p => p.Id).ToArray(), cancellationToken);
        return providers.Select(p => ToSummary(p, ratings)).ToList();
    }

    public async Task<ProviderDetail> GetPublicAsync(Guid id, CancellationToken cancellationToken)
    {
        var provider = await VerifiedQuery().SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new DomainException("Instructor not found.", 404);
        var ratings = await LoadRatingsAsync(new[] { id }, cancellationToken);
        return ToDetail(provider, ratings);
    }

    public async Task<SlotListResponse> GetSlotsAsync(
        Guid providerId,
        string? mode,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var exists = await _db.Providers.AnyAsync(
            p => p.Id == providerId && p.Status == ProviderStatus.Verified,
            cancellationToken);
        if (!exists)
            throw new DomainException("Instructor not found.", 404);

        var parsed = ParseRequiredMode(mode);
        var (start, end) = ResolveWindow(from, to);

        var taken = _db.Bookings
            .Where(b => b.ProviderId == providerId && Occupying.Contains(b.Status))
            .Select(b => b.SlotId);

        var slots = await _db.AvailabilitySlots.AsNoTracking()
            .Where(s => s.ProviderId == providerId && s.Mode == parsed && !s.IsBlocked)
            .Where(s => s.Date >= start && s.Date <= end)
            .Where(s => !taken.Contains(s.Id))
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        return new SlotListResponse(parsed.ToString(), start, end, slots.Select(ToSlot).ToList());
    }

    public async Task<RegisterProviderResponse> RegisterAsync(RegisterProviderRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == _current.UserId, cancellationToken)
            ?? throw new DomainException("Sign in required.", 401);
        if (user.Role == UserRole.Admin)
            throw new DomainException("Admin accounts cannot register as instructors.", 403);
        if (await _db.Providers.AnyAsync(p => p.UserId == user.Id, cancellationToken))
            throw new DomainException("This account is already registered as an instructor.", 409);

        var displayName = (request.DisplayName ?? "").Trim();
        if (displayName.Length is < 2 or > 80)
            throw new DomainException("Display name must be 2 to 80 characters.");
        if (request.Age is < 18 or > 80)
            throw new DomainException("Age must be between 18 and 80.");
        if (!request.OffersHome && !request.OffersStudio && !request.OffersOnline)
            throw new DomainException("Choose at least one session mode: Home, Studio, or Online.");

        var area = await _db.Areas.SingleOrDefaultAsync(a => a.Id == request.AreaId && a.IsActive, cancellationToken)
            ?? throw new DomainException("Choose a Mumbai area.");

        var category = request.CategoryId is Guid categoryId
            ? await _db.Categories.SingleOrDefaultAsync(c => c.Id == categoryId && c.IsActive, cancellationToken)
                ?? throw new DomainException("Unknown category.")
            : await _db.Categories.SingleOrDefaultAsync(c => c.Slug == "yoga" && c.IsActive, cancellationToken)
                ?? throw new DomainException("The yoga category is not available.");

        var email = NormalizeEmail(request.Email);
        var bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        if (bio is { Length: > 1000 })
            throw new DomainException("Bio must be 1000 characters or less.");

        var studioAddress = string.IsNullOrWhiteSpace(request.StudioAddress) ? null : request.StudioAddress.Trim();
        if (request.OffersStudio && string.IsNullOrWhiteSpace(studioAddress))
            throw new DomainException("Studio sessions need the studio address.");
        if (studioAddress is { Length: > 300 })
            throw new DomainException("Studio address must be 300 characters or less.");

        var meetLink = request.OffersOnline ? RequireMeetLink(request.GoogleMeetLink) : null;

        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Status = ProviderStatus.Pending,
            DisplayName = displayName,
            Bio = bio,
            Age = request.Age,
            AreaId = area.Id,
            Area = area,
            OffersHome = request.OffersHome,
            OffersStudio = request.OffersStudio,
            OffersOnline = request.OffersOnline,
            HomeRate = request.OffersHome ? RequireRate(request.HomeRate, "Home") : null,
            StudioRate = request.OffersStudio ? RequireRate(request.StudioRate, "Studio") : null,
            OnlineRate = request.OffersOnline ? RequireRate(request.OnlineRate, "Online") : null,
            StudioAddress = request.OffersStudio ? studioAddress : null,
            GoogleMeetLink = meetLink,
            CreatedAt = DateTimeOffset.UtcNow
        };

        user.Role = UserRole.Provider;
        if (email is not null)
            user.Email = email;
        if (string.IsNullOrWhiteSpace(user.Name))
            user.Name = displayName;

        _db.Providers.Add(provider);
        _db.Services.Add(new Service
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            CategoryId = category.Id,
            Title = category.Name + " session",
            IsActive = true
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new RegisterProviderResponse(ToSelf(provider, user, area.Name), _tokens.Create(user));
    }

    public async Task<ProviderSelf> GetMineAsync(CancellationToken cancellationToken)
    {
        var provider = await _db.Providers
            .Include(p => p.User)
            .Include(p => p.Area)
            .SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
            ?? throw new DomainException("Register as an instructor first.", 404);

        return ToSelf(provider, provider.User!, provider.Area!.Name);
    }

    public async Task<IReadOnlyList<SlotResponse>> AddSlotsAsync(AddSlotsRequest request, CancellationToken cancellationToken)
    {
        var provider = await _db.Providers.SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
            ?? throw new DomainException("Register as an instructor before adding availability.", 404);

        var mode = ParseRequiredMode(request.Mode);
        if (!provider.Offers(mode))
            throw new DomainException($"Turn on {mode} sessions before adding {mode} slots.");
        if (request.Slots is null || request.Slots.Count == 0)
            throw new DomainException("Add at least one slot.");
        if (request.Slots.Count > 40)
            throw new DomainException("Add at most 40 slots at a time.");

        var today = MumbaiClock.Today();
        var created = new List<AvailabilitySlot>();
        foreach (var input in request.Slots)
        {
            if (!TimeOnly.TryParseExact(input.Start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
                throw new DomainException("Start time must be HH:mm.");
            if (!TimeOnly.TryParseExact(input.End, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
                throw new DomainException("End time must be HH:mm.");
            if (end <= start)
                throw new DomainException("End time must be after the start time.");
            if (input.Date < today)
                throw new DomainException("Slots cannot be in the past.");

            var duplicate = created.Any(s => s.Date == input.Date && s.StartTime == start)
                || await _db.AvailabilitySlots.AnyAsync(s =>
                    s.ProviderId == provider.Id && s.Mode == mode && s.Date == input.Date && s.StartTime == start,
                    cancellationToken);
            if (duplicate)
                throw new DomainException($"A {mode} slot at {input.Date:yyyy-MM-dd} {input.Start} already exists.");

            created.Add(new AvailabilitySlot
            {
                Id = Guid.NewGuid(),
                ProviderId = provider.Id,
                Mode = mode,
                Date = input.Date,
                StartTime = start,
                EndTime = end
            });
        }

        _db.AvailabilitySlots.AddRange(created);
        await _db.SaveChangesAsync(cancellationToken);
        return created.Select(ToSlot).ToList();
    }

    public async Task<OwnedSlotListResponse> GetMySlotsAsync(
        string? mode,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var provider = await RequireCurrentProviderAsync(cancellationToken);
        var parsed = ParseRequiredMode(mode);
        var (start, end) = ResolveWindow(from, to);

        var slots = await _db.AvailabilitySlots.AsNoTracking()
            .Where(s => s.ProviderId == provider.Id && s.Mode == parsed)
            .Where(s => s.Date >= start && s.Date <= end)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        return new OwnedSlotListResponse(parsed.ToString(), start, end, slots.Select(ToOwnedSlot).ToList());
    }

    public async Task<OwnedSlotResponse> BlockSlotAsync(Guid slotId, CancellationToken cancellationToken)
    {
        var provider = await RequireCurrentProviderAsync(cancellationToken);
        var slot = await _db.AvailabilitySlots.SingleOrDefaultAsync(s => s.Id == slotId, cancellationToken)
            ?? throw new DomainException("Slot not found.", 404);
        if (slot.ProviderId != provider.Id)
            throw new DomainException("This slot belongs to another instructor.", 403);

        var taken = await _db.Bookings.AnyAsync(
            b => b.SlotId == slot.Id && Occupying.Contains(b.Status),
            cancellationToken);
        AvailabilityRules.Block(slot, taken, MumbaiClock.Today());
        await _db.SaveChangesAsync(cancellationToken);
        return ToOwnedSlot(slot);
    }

    private async Task<Provider> RequireCurrentProviderAsync(CancellationToken cancellationToken) =>
        await _db.Providers.SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
            ?? throw new DomainException("Register as an instructor first.", 404);

    private static (DateOnly Start, DateOnly End) ResolveWindow(DateOnly? from, DateOnly? to)
    {
        var start = from ?? MumbaiClock.Today();
        var end = to ?? start.AddDays(6);
        if (end < start)
            throw new DomainException("The end date is before the start date.");
        return (start, end);
    }

    private IQueryable<Provider> VerifiedQuery() =>
        _db.Providers.AsNoTracking()
            .Include(p => p.Area)
            .Include(p => p.Services).ThenInclude(s => s.Category)
            .Where(p => p.Status == ProviderStatus.Verified);

    private async Task<Dictionary<Guid, (decimal Average, int Count)>> LoadRatingsAsync(Guid[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
            return new Dictionary<Guid, (decimal, int)>();

        var rows = await _db.Reviews.AsNoTracking()
            .Where(r => ids.Contains(r.ProviderId))
            .GroupBy(r => r.ProviderId)
            .Select(g => new { g.Key, Average = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Key, r => (decimal.Round((decimal)r.Average, 2), r.Count));
    }

    private static ProviderSummary ToSummary(Provider provider, IReadOnlyDictionary<Guid, (decimal Average, int Count)> ratings)
    {
        ratings.TryGetValue(provider.Id, out var rating);
        return new ProviderSummary(
            provider.Id,
            provider.DisplayName,
            provider.Bio,
            provider.Area!.Name,
            provider.Area.City,
            provider.Status.ToString(),
            ModesOf(provider),
            rating.Count == 0 ? null : rating.Average,
            rating.Count);
    }

    private static ProviderDetail ToDetail(Provider provider, IReadOnlyDictionary<Guid, (decimal Average, int Count)> ratings)
    {
        ratings.TryGetValue(provider.Id, out var rating);
        return new ProviderDetail(
            provider.Id,
            provider.DisplayName,
            provider.Bio,
            provider.Age,
            provider.Area!.Name,
            provider.Area.City,
            provider.Status.ToString(),
            ModesOf(provider),
            provider.StudioAddress,
            rating.Count == 0 ? null : rating.Average,
            rating.Count);
    }

    private static ProviderSelf ToSelf(Provider provider, User user, string areaName) => new(
        provider.Id,
        provider.UserId,
        provider.DisplayName,
        provider.Bio,
        provider.Age,
        user.Email,
        provider.AreaId,
        areaName,
        "Mumbai",
        provider.Status.ToString(),
        provider.OffersHome,
        provider.OffersStudio,
        provider.OffersOnline,
        provider.HomeRate,
        provider.StudioRate,
        provider.OnlineRate,
        provider.StudioAddress,
        provider.GoogleMeetLink,
        provider.RejectionReason);

    private static List<ModeRate> ModesOf(Provider provider)
    {
        var modes = new List<ModeRate>();
        if (provider.OffersHome && provider.HomeRate is decimal home)
            modes.Add(new ModeRate(nameof(SessionMode.Home), home));
        if (provider.OffersStudio && provider.StudioRate is decimal studio)
            modes.Add(new ModeRate(nameof(SessionMode.Studio), studio));
        if (provider.OffersOnline && provider.OnlineRate is decimal online)
            modes.Add(new ModeRate(nameof(SessionMode.Online), online));
        return modes;
    }

    private static SlotResponse ToSlot(AvailabilitySlot slot) => new(
        slot.Id,
        slot.Mode.ToString(),
        slot.Date,
        slot.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
        slot.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture));

    private static OwnedSlotResponse ToOwnedSlot(AvailabilitySlot slot) => new(
        slot.Id,
        slot.Mode.ToString(),
        slot.Date,
        slot.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
        slot.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
        slot.IsBlocked);

    private static SessionMode? ParseOptionalMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return null;
        return ParseRequiredMode(mode);
    }

    private static SessionMode ParseRequiredMode(string? mode)
    {
        if (!Enum.TryParse<SessionMode>(mode, true, out var parsed))
            throw new DomainException("Mode must be Home, Studio, or Online.");
        return parsed;
    }

    private static decimal RequireRate(decimal? rate, string mode)
    {
        if (rate is null or <= 0 or > 100000)
            throw new DomainException($"{mode} sessions need a rate in INR.");
        return rate.Value;
    }

    private static string RequireMeetLink(string? link)
    {
        var value = (link ?? "").Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals("meet.google.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Online sessions need an https://meet.google.com link.");
        }

        return value;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        var value = email.Trim();
        if (value.Length > 200 || !value.Contains('@', StringComparison.Ordinal) || value.StartsWith('@') || value.EndsWith('@'))
            throw new DomainException("Enter a valid email.");
        return value;
    }
}
