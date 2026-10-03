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
        string? city,
        string? area,
        string? mode,
        string? category,
        CancellationToken cancellationToken)
    {
        var parsedMode = ParseOptionalMode(mode);
        var query = VerifiedQuery().Where(p => p.Area!.IsActive);

        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityName = city.Trim().ToLower();
            query = query.Where(p => p.Area!.City.ToLower() == cityName);
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            var name = area.Trim().ToLower();
            query = query.Where(p => p.Area!.Name.ToLower() == name);
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
        await RequireListedAsync(providerId, cancellationToken);

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
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.Area)
            .SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
            ?? throw new DomainException("Register as an instructor first.", 404);

        return ToSelf(provider, provider.User!, provider.Area!.Name);
    }

    public async Task<ProviderSelf> UpdateRatesAsync(UpdateProviderRatesRequest request, CancellationToken cancellationToken)
    {
        var provider = await _db.Providers
            .Include(p => p.User)
            .Include(p => p.Area)
            .SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
            ?? throw new DomainException("Register as an instructor first.", 404);

        if (request.HomeRate is null && request.StudioRate is null && request.OnlineRate is null)
            throw new DomainException("Send at least one rate to update.");

        if (request.HomeRate is decimal home)
        {
            if (!provider.OffersHome)
                throw new DomainException("Turn on Home sessions before setting a Home rate.");
            provider.HomeRate = RequireRate(home, "Home");
        }

        if (request.StudioRate is decimal studio)
        {
            if (!provider.OffersStudio)
                throw new DomainException("Turn on Studio sessions before setting a Studio rate.");
            provider.StudioRate = RequireRate(studio, "Studio");
        }

        if (request.OnlineRate is decimal online)
        {
            if (!provider.OffersOnline)
                throw new DomainException("Turn on Online sessions before setting an Online rate.");
            provider.OnlineRate = RequireRate(online, "Online");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToSelf(provider, provider.User!, provider.Area!.Name);
    }

    public async Task<ProviderSelf> UpdateProfileAsync(UpdateProviderProfileRequest request, CancellationToken cancellationToken)
    {
        var provider = await _db.Providers
            .Include(p => p.User)
            .Include(p => p.Area)
            .SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
            ?? throw new DomainException("Register as an instructor first.", 404);

        var user = provider.User ?? throw new InvalidOperationException("User was not loaded.");
        var displayName = (request.DisplayName ?? "").Trim();
        if (displayName.Length is < 2 or > 80)
            throw new DomainException("Display name must be 2 to 80 characters.");
        if (request.Age is < 18 or > 80)
            throw new DomainException("Age must be between 18 and 80.");
        if (!request.OffersHome && !request.OffersStudio && !request.OffersOnline)
            throw new DomainException("Choose at least one session mode: Home, Studio, or Online.");

        var area = await _db.Areas.SingleOrDefaultAsync(a => a.Id == request.AreaId && a.IsActive, cancellationToken)
            ?? throw new DomainException("Choose a Mumbai area.");

        if (provider.OffersHome && !request.OffersHome
            && await HasLiveBookingsAsync(provider.Id, SessionMode.Home, cancellationToken))
            throw new DomainException("Home sessions still have upcoming or pending bookings.", 409);
        if (provider.OffersStudio && !request.OffersStudio
            && await HasLiveBookingsAsync(provider.Id, SessionMode.Studio, cancellationToken))
            throw new DomainException("Studio sessions still have upcoming or pending bookings.", 409);
        if (provider.OffersOnline && !request.OffersOnline
            && await HasLiveBookingsAsync(provider.Id, SessionMode.Online, cancellationToken))
            throw new DomainException("Online sessions still have upcoming or pending bookings.", 409);

        var email = NormalizeEmail(request.Email);
        var bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        if (bio is { Length: > 1000 })
            throw new DomainException("Bio must be 1000 characters or less.");

        var studioAddress = string.IsNullOrWhiteSpace(request.StudioAddress) ? null : request.StudioAddress.Trim();
        if (request.OffersStudio && string.IsNullOrWhiteSpace(studioAddress))
            throw new DomainException("Studio sessions need the studio address.");
        if (studioAddress is { Length: > 300 })
            throw new DomainException("Studio address must be 300 characters or less.");

        var meetLink = request.OffersOnline ? RequireMeetLink(request.GoogleMeetLink) : provider.GoogleMeetLink;

        provider.DisplayName = displayName;
        provider.Age = request.Age;
        provider.Bio = bio;
        provider.AreaId = area.Id;
        provider.Area = area;
        provider.OffersHome = request.OffersHome;
        provider.OffersStudio = request.OffersStudio;
        provider.OffersOnline = request.OffersOnline;
        provider.HomeRate = request.OffersHome ? RequireRate(request.HomeRate, "Home") : provider.HomeRate;
        provider.StudioRate = request.OffersStudio ? RequireRate(request.StudioRate, "Studio") : provider.StudioRate;
        provider.OnlineRate = request.OffersOnline ? RequireRate(request.OnlineRate, "Online") : provider.OnlineRate;
        if (request.OffersStudio)
            provider.StudioAddress = studioAddress;
        if (request.OffersOnline)
            provider.GoogleMeetLink = meetLink;

        user.Email = email;
        user.Name = displayName;

        await _db.SaveChangesAsync(cancellationToken);
        return ToSelf(provider, user, area.Name);
    }

    public async Task<IReadOnlyList<InstructorPayoutResponse>> ListMyPayoutsAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var provider = await _db.Providers.AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
            ?? throw new DomainException("Register as an instructor first.", 404);

        var query = _db.PayoutsPending.AsNoTracking().Include(p => p.Booking).Where(p => p.ProviderId == provider.Id);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<PayoutStatus>(status, true, out var parsed))
                throw new DomainException("Unknown payout status.");
            query = query.Where(p => p.Status == parsed);
        }

        var rows = await query.ToListAsync(cancellationToken);
        return rows
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .Select(p => new InstructorPayoutResponse(
                p.Id,
                p.BookingId,
                p.GrossAmount,
                p.FeePercent,
                p.FeeAmount,
                p.NetAmount,
                p.Status.ToString(),
                p.CreatedAt,
                p.Booking?.Status.ToString() ?? ""))
            .ToList();
    }

    public async Task<IReadOnlyList<PublicReviewResponse>> ListPublicReviewsAsync(
        Guid providerId,
        CancellationToken cancellationToken)
    {
        await RequireListedAsync(providerId, cancellationToken);

        var rows = await (
                from review in _db.Reviews.AsNoTracking()
                join user in _db.Users.AsNoTracking() on review.CustomerId equals user.Id
                where review.ProviderId == providerId
                select new { review, user.Name })
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(row => row.review.CreatedAt)
            .Take(50)
            .Select(row => new PublicReviewResponse(
                row.review.Rating,
                row.review.Comment,
                PublicReviewerName(row.Name),
                row.review.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<SlotResponse>> AddSlotsAsync(AddSlotsRequest request, CancellationToken cancellationToken)
    {
        var provider = await _db.Providers.SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
            ?? throw new DomainException("Register as an instructor before adding availability.", 404);
        RequireVerifiedForSlotChanges(provider);

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

    public async Task<OwnedSlotResponse> UpdateSlotAsync(
        Guid slotId,
        UpdateSlotRequest request,
        CancellationToken cancellationToken)
    {
        var slot = await RequireOwnedSlotAsync(slotId, cancellationToken);
        var taken = await SlotIsOccupiedAsync(slot.Id, cancellationToken);
        if (!TimeOnly.TryParseExact(request.Start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new DomainException("Start time must be HH:mm.");
        if (!TimeOnly.TryParseExact(request.End, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            throw new DomainException("End time must be HH:mm.");

        AvailabilityRules.Update(slot, taken, MumbaiClock.Today(), request.Date, start, end);

        var clash = await _db.AvailabilitySlots.AnyAsync(
            s => s.Id != slot.Id
                && s.ProviderId == slot.ProviderId
                && s.Mode == slot.Mode
                && s.Date == request.Date
                && s.StartTime == start,
            cancellationToken);
        if (clash)
            throw new DomainException($"A {slot.Mode} slot at {request.Date:yyyy-MM-dd} {request.Start} already exists.");

        await _db.SaveChangesAsync(cancellationToken);
        return ToOwnedSlot(slot);
    }

    public async Task<OwnedSlotResponse> BlockSlotAsync(Guid slotId, CancellationToken cancellationToken)
    {
        var slot = await RequireOwnedSlotAsync(slotId, cancellationToken);
        var taken = await SlotIsOccupiedAsync(slot.Id, cancellationToken);
        AvailabilityRules.Block(slot, taken, MumbaiClock.Today());
        await _db.SaveChangesAsync(cancellationToken);
        return ToOwnedSlot(slot);
    }

    public async Task<OwnedSlotResponse> UnblockSlotAsync(Guid slotId, CancellationToken cancellationToken)
    {
        var slot = await RequireOwnedSlotAsync(slotId, cancellationToken);
        var taken = await SlotIsOccupiedAsync(slot.Id, cancellationToken);
        AvailabilityRules.Unblock(slot, taken, MumbaiClock.Today());
        await _db.SaveChangesAsync(cancellationToken);
        return ToOwnedSlot(slot);
    }

    public async Task DeleteSlotAsync(Guid slotId, CancellationToken cancellationToken)
    {
        var slot = await RequireOwnedSlotAsync(slotId, cancellationToken);
        var referenced = await _db.Bookings.AnyAsync(b => b.SlotId == slot.Id, cancellationToken)
            || await _db.CheckoutIntents.AnyAsync(c => c.SlotId == slot.Id, cancellationToken);
        AvailabilityRules.Delete(referenced);
        _db.AvailabilitySlots.Remove(slot);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AvailabilitySlot> RequireOwnedSlotAsync(Guid slotId, CancellationToken cancellationToken)
    {
        var provider = await RequireCurrentProviderAsync(cancellationToken);
        RequireVerifiedForSlotChanges(provider);
        var slot = await _db.AvailabilitySlots.SingleOrDefaultAsync(s => s.Id == slotId, cancellationToken)
            ?? throw new DomainException("Slot not found.", 404);
        if (slot.ProviderId != provider.Id)
            throw new DomainException("This slot belongs to another instructor.", 403);
        return slot;
    }

    private Task<bool> SlotIsOccupiedAsync(Guid slotId, CancellationToken cancellationToken) =>
        _db.Bookings.AnyAsync(b => b.SlotId == slotId && Occupying.Contains(b.Status), cancellationToken);

    private static readonly BookingStatus[] LiveOccupying =
    {
        BookingStatus.PendingAccept,
        BookingStatus.Upcoming
    };

    private Task<bool> HasLiveBookingsAsync(Guid providerId, SessionMode mode, CancellationToken cancellationToken) =>
        _db.Bookings.AnyAsync(
            b => b.ProviderId == providerId && b.Mode == mode && LiveOccupying.Contains(b.Status),
            cancellationToken);

    private static string PublicReviewerName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Student";
        var trimmed = name.Trim();
        var space = trimmed.IndexOf(' ');
        return space < 0 ? trimmed : trimmed[..space];
    }

    private async Task<Provider> RequireCurrentProviderAsync(CancellationToken cancellationToken) =>
        await _db.Providers.SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
            ?? throw new DomainException("Register as an instructor first.", 404);

    private static void RequireVerifiedForSlotChanges(Provider provider)
    {
        if (provider.Status != ProviderStatus.Verified)
            throw new DomainException("Your profile must be verified before you can change slots.", 403);
    }

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
            .Where(ProviderApproval.IsListed);

    private async Task RequireListedAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var listed = await _db.Providers.Where(p => p.Id == providerId).AnyAsync(ProviderApproval.IsListed, cancellationToken);
        if (!listed)
            throw new DomainException("Instructor not found.", 404);
    }

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
