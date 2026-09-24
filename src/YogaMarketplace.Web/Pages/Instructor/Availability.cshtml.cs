using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Instructor;

public class AvailabilityModel : PageModel
{
    private readonly IInstructorAvailabilityApi _slots;
    private readonly IInstructorBookingApi _bookings;

    public AvailabilityModel(IInstructorAvailabilityApi slots, IInstructorBookingApi bookings)
    {
        _slots = slots;
        _bookings = bookings;
    }

    public string Mode { get; private set; } = SessionModes.Home;
    public bool ModeKnown { get; private set; } = true;
    public string? FromQuery { get; private set; }
    public string? ToQuery { get; private set; }
    public DateOnly? WindowFrom { get; private set; }
    public DateOnly? WindowTo { get; private set; }
    public string? WindowLabel { get; private set; }
    public List<OwnedSlotDto> Slots { get; private set; } = [];
    public HashSet<Guid> OccupiedSlotIds { get; private set; } = [];
    public string? Error { get; private set; }
    public string? Notice { get; private set; }

    public async Task OnGetAsync(string? mode, string? from, string? to, string? notice, CancellationToken cancellationToken)
    {
        Notice = InstructorAvailabilityNotices.Text(notice);
        await LoadAsync(mode, from, to, cancellationToken);
    }

    public async Task<IActionResult> OnPostBlockAsync(
        Guid id,
        string? mode,
        string? from,
        string? to,
        CancellationToken cancellationToken)
    {
        var result = await _slots.BlockAsync(id, cancellationToken);
        var canonical = SessionModes.Normalize(mode) ?? SessionModes.Home;
        var fromQuery = BlankToNull(from);
        var toQuery = BlankToNull(to);
        if (!result.Ok || result.Data is null)
        {
            var error = result.Error ?? UiCopy.GenericError;
            if (WantsFragment)
                return Fragment(new SlotMutationBody(false, null, error, null));

            Error = error;
            await LoadAsync(canonical, from, to, cancellationToken);
            return Page();
        }

        if (WantsFragment)
        {
            Mode = canonical;
            FromQuery = fromQuery;
            ToQuery = toQuery;
            var slot = result.Data;
            return Fragment(new SlotMutationBody(
                true,
                InstructorAvailabilityNotices.Text(InstructorAvailabilityNotices.Blocked),
                null,
                new SlotCardView(
                    slot,
                    InstructorAvailability.State(slot, occupied: false),
                    InstructorAvailability.CanBlock(slot, occupied: false),
                    canonical,
                    fromQuery,
                    toQuery)));
        }

        return RedirectToPage(new
        {
            mode = canonical,
            from = fromQuery,
            to = toQuery,
            notice = InstructorAvailabilityNotices.Blocked
        });
    }

    public SlotCardView CardFor(OwnedSlotDto slot) =>
        new(slot, StateOf(slot), CanBlock(slot), Mode, FromQuery, ToQuery);

    private PartialViewResult Fragment(SlotMutationBody body)
    {
        Response.Headers.CacheControl = "no-store";
        return Partial("_SlotMutation", body);
    }

    private bool WantsFragment =>
        string.Equals(Request.Headers["X-Requested-With"], "fetch", StringComparison.OrdinalIgnoreCase);

    public bool IsOccupied(OwnedSlotDto slot) => OccupiedSlotIds.Contains(slot.Id);

    public string StateOf(OwnedSlotDto slot) => InstructorAvailability.State(slot, IsOccupied(slot));

    public bool CanBlock(OwnedSlotDto slot) => InstructorAvailability.CanBlock(slot, IsOccupied(slot));

    public IEnumerable<IGrouping<DateOnly, OwnedSlotDto>> Days =>
        Slots.GroupBy(slot => slot.Date);

    private async Task LoadAsync(string? mode, string? from, string? to, CancellationToken cancellationToken)
    {
        FromQuery = BlankToNull(from);
        ToQuery = BlankToNull(to);

        if (string.IsNullOrWhiteSpace(mode))
        {
            Mode = SessionModes.Home;
            ModeKnown = true;
        }
        else if (SessionModes.Normalize(mode) is string canonical)
        {
            Mode = canonical;
            ModeKnown = true;
        }
        else
        {
            ModeKnown = false;
            Error ??= UiCopy.ModeRequired;
            Slots = [];
            return;
        }

        if (!AdminInput.TryDateRange(FromQuery, ToQuery, out var fromDate, out var toDate, out var dateError))
        {
            Error ??= dateError;
            Slots = [];
            return;
        }

        var list = await _slots.ListAsync(Mode, fromDate, toDate, cancellationToken);
        if (!list.Ok || list.Data is null)
        {
            Error ??= list.Error ?? UiCopy.GenericError;
            Slots = [];
            return;
        }

        WindowFrom = list.Data.From;
        WindowTo = list.Data.To;
        WindowLabel = InstructorAvailability.WindowLabel(list.Data.From, list.Data.To);
        Slots = (list.Data.Slots ?? [])
            .OrderBy(slot => slot.Date)
            .ThenBy(slot => slot.Start, StringComparer.Ordinal)
            .ToList();

        var bookings = await _bookings.ListAsync(null, cancellationToken);
        if (!bookings.Ok || bookings.Data is null)
        {
            Error ??= bookings.Error ?? UiCopy.GenericError;
            OccupiedSlotIds = [];
            return;
        }

        OccupiedSlotIds = bookings.Data
            .Where(booking => InstructorAvailability.Occupies(booking.Status))
            .Select(booking => booking.SlotId)
            .ToHashSet();
    }

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SlotCardView(
    OwnedSlotDto Slot,
    string State,
    bool CanBlock,
    string Mode,
    string? FromQuery,
    string? ToQuery);

public sealed record SlotMutationBody(
    bool Ok,
    string? Notice,
    string? Error,
    SlotCardView? Card);
