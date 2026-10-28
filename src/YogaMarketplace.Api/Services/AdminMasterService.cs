using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public interface IAdminMasterService
{
    Task<IReadOnlyList<AdminAreaResponse>> ListAreasAsync(CancellationToken cancellationToken);

    Task<AdminAreaResponse> CreateAreaAsync(CreateAreaRequest request, CancellationToken cancellationToken);

    Task<AdminAreaResponse> UpdateAreaAsync(Guid id, PatchAreaRequest request, CancellationToken cancellationToken);

    Task<PagedResponse<AdminStateResponse>> ListStatesAsync(int? page, int? pageSize, CancellationToken cancellationToken);

    Task<AdminStateResponse> CreateStateAsync(CreateStateRequest request, CancellationToken cancellationToken);

    Task<AdminStateResponse> UpdateStateAsync(Guid id, PatchStateRequest request, CancellationToken cancellationToken);

    Task<LocationImportResponse> ImportIndiaLocationsAsync(CancellationToken cancellationToken);

    Task<PagedResponse<AdminCityResponse>> ListCitiesAsync(Guid? stateId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<AdminCityResponse> CreateCityAsync(CreateCityRequest request, CancellationToken cancellationToken);

    Task<AdminCityResponse> UpdateCityAsync(Guid id, PatchCityRequest request, CancellationToken cancellationToken);

    Task DeleteCityAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminCategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken);

    Task<AdminCategoryResponse> UpdateCategoryAsync(Guid id, PatchCategoryRequest request, CancellationToken cancellationToken);
}

public class AdminMasterService : IAdminMasterService
{
    private readonly YogaDbContext _db;
    private readonly IWebHostEnvironment _env;

    public AdminMasterService(YogaDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IReadOnlyList<AdminAreaResponse>> ListAreasAsync(CancellationToken cancellationToken) =>
        await _db.Areas.AsNoTracking()
            .OrderBy(a => a.City!.State!.Name)
            .ThenBy(a => a.City!.Name)
            .ThenBy(a => a.Name)
            .Select(a => new AdminAreaResponse(
                a.Id, a.CityId, a.City!.Name, a.City.StateId, a.City.State!.Name, a.Name, a.Pincode, a.Latitude, a.Longitude, a.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<AdminAreaResponse> CreateAreaAsync(CreateAreaRequest request, CancellationToken cancellationToken)
    {
        var area = CatalogRules.CreateArea(request.CityId ?? Guid.Empty, request.Name, request.Pincode, request.Latitude, request.Longitude);
        await RequireActiveCityAsync(area.CityId, cancellationToken);
        await EnsureAreaNameAvailableAsync(area.CityId, area.Name, null, cancellationToken);
        _db.Areas.Add(area);
        await _db.SaveChangesAsync(cancellationToken);
        return await ToAreaAsync(area.Id, cancellationToken);
    }

    public async Task<AdminAreaResponse> UpdateAreaAsync(Guid id, PatchAreaRequest request, CancellationToken cancellationToken)
    {
        if (request.Name is null && request.CityId is null && request.Pincode is null
            && request.Latitude is null && request.Longitude is null && request.IsActive is null)
            throw new DomainException("Send a name, city, PIN, coordinates, or isActive.");

        var area = await _db.Areas.SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new DomainException("Area not found.", 404);

        if (request.CityId is Guid cityId)
        {
            await RequireActiveCityAsync(cityId, cancellationToken);
            area.CityId = cityId;
        }

        if (request.Name is not null)
            CatalogRules.RenameArea(area, request.Name);

        if (request.Pincode is not null)
            area.Pincode = CatalogRules.NormalizePincode(request.Pincode);

        if (request.Latitude is not null || request.Longitude is not null)
            CatalogRules.SetCoordinates(area, request.Latitude ?? area.Latitude, request.Longitude ?? area.Longitude);

        if (request.Name is not null || request.CityId is not null)
            await EnsureAreaNameAvailableAsync(area.CityId, area.Name, area.Id, cancellationToken);

        if (request.IsActive is bool active)
            area.IsActive = active;

        await _db.SaveChangesAsync(cancellationToken);
        return await ToAreaAsync(area.Id, cancellationToken);
    }

    public async Task<PagedResponse<AdminStateResponse>> ListStatesAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (pageNumber, size) = AdminQuery.Paging(page, pageSize);
        var query = _db.States.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(s => new AdminStateResponse(s.Id, s.Name, s.Code, s.IsActive, s.DisplayOrder))
            .ToListAsync(cancellationToken);
        return new PagedResponse<AdminStateResponse>(items, total, pageNumber, size);
    }

    public async Task<AdminStateResponse> CreateStateAsync(CreateStateRequest request, CancellationToken cancellationToken)
    {
        var state = CatalogRules.CreateState(request.Name, request.Code, request.DisplayOrder ?? 0);
        await EnsureStateNameAvailableAsync(state.Name, null, cancellationToken);
        await EnsureStateCodeAvailableAsync(state.Code, null, cancellationToken);
        _db.States.Add(state);
        await _db.SaveChangesAsync(cancellationToken);
        return ToState(state);
    }

    public async Task<AdminStateResponse> UpdateStateAsync(Guid id, PatchStateRequest request, CancellationToken cancellationToken)
    {
        if (request.Name is null && request.Code is null && request.DisplayOrder is null && request.IsActive is null)
            throw new DomainException("Send a name, code, display order, or isActive.");

        var state = await _db.States.SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new DomainException("State not found.", 404);

        if (request.Name is not null)
        {
            state.Name = CatalogRules.NormalizeStateName(request.Name);
            await EnsureStateNameAvailableAsync(state.Name, state.Id, cancellationToken);
        }

        if (request.Code is not null)
        {
            state.Code = CatalogRules.NormalizeStateCode(request.Code);
            await EnsureStateCodeAvailableAsync(state.Code, state.Id, cancellationToken);
            var cities = await _db.Cities.Where(c => c.StateId == state.Id).ToListAsync(cancellationToken);
            foreach (var city in cities)
            {
                city.Slug = CatalogRules.CitySlug(state.Code, city.Name);
                await EnsureCityAvailableAsync(city, cancellationToken);
            }
        }

        if (request.DisplayOrder is int order)
            state.DisplayOrder = order;

        if (request.IsActive is false)
            await EnsureStateCanTurnOffAsync(state.Id, cancellationToken);

        if (request.IsActive is bool active)
            state.IsActive = active;

        await _db.SaveChangesAsync(cancellationToken);
        return ToState(state);
    }

    public async Task<LocationImportResponse> ImportIndiaLocationsAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(_env.ContentRootPath, "SeedData", "locations-in.json");
        if (!File.Exists(path))
            throw new DomainException("The India location file is missing.");

        await using var stream = File.OpenRead(path);
        var file = await JsonSerializer.DeserializeAsync<IndiaLocationFile>(stream, JsonOptions, cancellationToken)
            ?? throw new DomainException("The India location file is empty.");

        var existingStates = await _db.States.ToListAsync(cancellationToken);
        var byName = existingStates.ToDictionary(s => s.Name);
        var byCode = existingStates.ToDictionary(s => s.Code);
        var cityKeys = (await _db.Cities.Select(c => new { c.StateId, c.Name }).ToListAsync(cancellationToken))
            .Select(c => (c.StateId, c.Name))
            .ToHashSet();
        var statesAdded = 0;
        var statesSkipped = 0;
        var citiesAdded = 0;
        var citiesSkipped = 0;
        var stateOrder = 0;
        foreach (var row in file.States)
        {
            State state;
            try
            {
                var name = CatalogRules.NormalizeStateName(row.Name);
                var code = CatalogRules.NormalizeStateCode(row.Code);
                if (byName.TryGetValue(name, out var existing) || byCode.TryGetValue(code, out existing))
                {
                    state = existing;
                    statesSkipped++;
                }
                else
                {
                    state = CatalogRules.CreateState(row.Name, row.Code, stateOrder);
                    state.IsActive = false;
                    _db.States.Add(state);
                    byName[state.Name] = state;
                    byCode[state.Code] = state;
                    statesAdded++;
                }
            }
            catch (DomainException)
            {
                statesSkipped++;
                stateOrder++;
                continue;
            }

            var cityOrder = 0;
            foreach (var cityName in row.Cities)
            {
                try
                {
                    var city = CatalogRules.CreateCity(cityName, state.Id, state.Code, cityOrder);
                    city.IsActive = false;
                    if (!cityKeys.Add((state.Id, city.Name)))
                        citiesSkipped++;
                    else
                    {
                        _db.Cities.Add(city);
                        citiesAdded++;
                    }
                }
                catch (DomainException)
                {
                    citiesSkipped++;
                }
                cityOrder++;
            }
            stateOrder++;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (UniqueViolation.Is(ex))
        {
            throw new DomainException("Another import saved the same states or cities first. Reload and check the list.", 409);
        }
        return new LocationImportResponse(statesAdded, statesSkipped, citiesAdded, citiesSkipped);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PagedResponse<AdminCityResponse>> ListCitiesAsync(
        Guid? stateId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (stateId is not Guid id || id == Guid.Empty)
            throw new DomainException("Pick a state.");

        var (pageNumber, size) = AdminQuery.Paging(page, pageSize);
        var query = _db.Cities.AsNoTracking().Where(c => c.StateId == id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(c => new AdminCityResponse(c.Id, c.Name, c.StateId, c.Slug, c.IsActive, c.DisplayOrder))
            .ToListAsync(cancellationToken);
        return new PagedResponse<AdminCityResponse>(items, total, pageNumber, size);
    }

    public async Task<AdminCityResponse> CreateCityAsync(CreateCityRequest request, CancellationToken cancellationToken)
    {
        if (request.StateId is not Guid stateId)
            throw new DomainException("Pick an active state.");
        var state = await RequireActiveStateAsync(stateId, cancellationToken);
        var city = CatalogRules.CreateCity(request.Name, state.Id, state.Code, request.DisplayOrder ?? 0);
        await EnsureCityAvailableAsync(city, cancellationToken);
        _db.Cities.Add(city);
        await _db.SaveChangesAsync(cancellationToken);
        return ToCity(city);
    }

    public async Task<AdminCityResponse> UpdateCityAsync(Guid id, PatchCityRequest request, CancellationToken cancellationToken)
    {
        if (request.Name is null && request.StateId is null && request.DisplayOrder is null && request.IsActive is null)
            throw new DomainException("Send a name, state, display order, or isActive.");

        var city = await _db.Cities.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new DomainException("City not found.", 404);

        if (request.StateId is Guid stateId)
        {
            var next = await RequireActiveStateAsync(stateId, cancellationToken);
            city.StateId = next.Id;
        }

        var stateCode = await _db.States.Where(s => s.Id == city.StateId).Select(s => s.Code).SingleAsync(cancellationToken);
        if (request.Name is not null || request.StateId is not null)
            CatalogRules.RenameCity(city, request.Name ?? city.Name, stateCode);

        if (request.DisplayOrder is int order)
            city.DisplayOrder = order;

        await EnsureCityAvailableAsync(city, cancellationToken);

        if (request.IsActive is bool active)
            city.IsActive = active;

        await _db.SaveChangesAsync(cancellationToken);
        return ToCity(city);
    }

    public async Task DeleteCityAsync(Guid id, CancellationToken cancellationToken)
    {
        var city = await _db.Cities.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new DomainException("City not found.", 404);

        if (await _db.Users.AnyAsync(u => u.PreferredCityId == id, cancellationToken))
            throw new DomainException("This city is in use. Turn it off instead of deleting it.", 409);

        if (await _db.Areas.AnyAsync(a => a.CityId == id, cancellationToken))
            throw new DomainException("Remove or move this city's neighbourhoods before deleting it.", 409);

        _db.Cities.Remove(city);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ForeignKeyViolation.Is(ex))
        {
            throw new DomainException("This city is in use. Turn it off instead of deleting it.", 409);
        }
    }

    public async Task<IReadOnlyList<AdminCategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new AdminCategoryResponse(c.Id, c.Name, c.Slug, c.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<AdminCategoryResponse> UpdateCategoryAsync(
        Guid id,
        PatchCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new DomainException("Unknown category.", 404);
        var slug = category.Slug;
        CatalogRules.RenameCategory(category, request.Name);
        category.Slug = slug;
        await _db.SaveChangesAsync(cancellationToken);
        return new AdminCategoryResponse(category.Id, category.Name, category.Slug, category.IsActive);
    }

    private async Task<City> RequireActiveCityAsync(Guid cityId, CancellationToken cancellationToken)
    {
        var city = await _db.Cities.Include(c => c.State).SingleOrDefaultAsync(c => c.Id == cityId, cancellationToken)
            ?? throw new DomainException("Pick an active city.");
        if (!city.IsActive)
            throw new DomainException("Pick an active city.");
        if (city.State is not { IsActive: true })
            throw new DomainException("Pick an active state.");
        return city;
    }

    private async Task EnsureAreaNameAvailableAsync(Guid cityId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        var matches = _db.Areas.Where(a => a.CityId == cityId && a.Name.ToLower() == lowered);
        if (exceptId is Guid id)
            matches = matches.Where(a => a.Id != id);
        var taken = await matches.AnyAsync(cancellationToken);
        if (taken)
            throw new DomainException("That area already exists.", 409);
    }

    private async Task<State> RequireActiveStateAsync(Guid stateId, CancellationToken cancellationToken)
    {
        var state = await _db.States.SingleOrDefaultAsync(s => s.Id == stateId && s.IsActive, cancellationToken);
        if (state is null)
            throw new DomainException("Pick an active state.");
        return state;
    }

    private async Task EnsureStateCanTurnOffAsync(Guid stateId, CancellationToken cancellationToken)
    {
        var hasActiveCity = await _db.Cities.AnyAsync(c => c.StateId == stateId && c.IsActive, cancellationToken);
        if (hasActiveCity)
            throw new DomainException("Turn off this state's cities before turning off the state.");
    }

    private async Task EnsureStateNameAvailableAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var matches = _db.States.Where(s => s.Name == name);
        if (exceptId is Guid id)
            matches = matches.Where(s => s.Id != id);
        if (await matches.AnyAsync(cancellationToken))
            throw new DomainException("That state already exists.", 409);
    }

    private async Task EnsureStateCodeAvailableAsync(string code, Guid? exceptId, CancellationToken cancellationToken)
    {
        var matches = _db.States.Where(s => s.Code == code);
        if (exceptId is Guid id)
            matches = matches.Where(s => s.Id != id);
        if (await matches.AnyAsync(cancellationToken))
            throw new DomainException("That state code is already used.", 409);
    }

    private async Task EnsureCityAvailableAsync(City city, CancellationToken cancellationToken)
    {
        var sameName = _db.Cities.Where(c => c.StateId == city.StateId && c.Name == city.Name && c.Id != city.Id);
        if (await sameName.AnyAsync(cancellationToken))
            throw new DomainException("That city already exists.", 409);

        var sameSlug = _db.Cities.Where(c => c.Slug == city.Slug && c.Id != city.Id);
        if (await sameSlug.AnyAsync(cancellationToken))
            throw new DomainException("That city slug is already used.", 409);
    }

    private async Task<AdminAreaResponse> ToAreaAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Areas.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AdminAreaResponse(
                a.Id, a.CityId, a.City!.Name, a.City.StateId, a.City.State!.Name, a.Name, a.Pincode, a.Latitude, a.Longitude, a.IsActive))
            .SingleAsync(cancellationToken);

    private static AdminStateResponse ToState(State state) =>
        new(state.Id, state.Name, state.Code, state.IsActive, state.DisplayOrder);

    private static AdminCityResponse ToCity(City city) =>
        new(city.Id, city.Name, city.StateId, city.Slug, city.IsActive, city.DisplayOrder);
}
