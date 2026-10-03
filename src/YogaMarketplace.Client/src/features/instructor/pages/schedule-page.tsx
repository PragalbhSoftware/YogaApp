import { useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, Typography } from "@mui/material";
import { Plus } from "lucide-react";
import { toast } from "sonner";
import { usePageTitle } from "@/hooks/use-page-title";
import { sessionModes, type SessionMode } from "@/constants/catalog";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { SlotWeekSkeleton } from "@/components/common/loading-skeleton";
import { PageLoader } from "@/components/common/page-loader";
import { ModeChips } from "@/features/instructor/components/mode-chips";
import { SlotCard } from "@/features/instructor/components/slot-card";
import { SlotForm } from "@/features/instructor/components/slot-form";
import { StatusBanner } from "@/features/instructor/components/status-banner";
import { countSlotsByDate, MonthCalendar } from "@/features/marketplace/components/month-calendar";
import {
  useAddSlots,
  useBlockSlot,
  useDeleteSlot,
  useInstructorBookings,
  useInstructorProfile,
  useInstructorSlots,
  useUnblockSlot,
  useUpdateSlot,
} from "@/features/instructor/hooks/use-instructor-schedule";
import {
  bookingOccupiesSlot,
  canManageSlots,
  offeredModes,
  slotState,
  type OwnedSlot,
} from "@/features/instructor/types";
import { toUserMessage } from "@/services/http/api-error";
import {
  clampDateToMonth,
  formatSlotDay,
  formatTimeRange12,
  isIsoDate,
  kolkataToday,
  monthQueryRange,
  startOfMonth,
} from "@/utils/clock";

function readMode(value: string | null, allowed: SessionMode[]): SessionMode {
  if (value && allowed.includes(value as SessionMode)) return value as SessionMode;
  return allowed[0] ?? "Home";
}

export function SchedulePage() {
  usePageTitle("Schedule");
  const profile = useInstructorProfile();
  const allowed = profile.data ? offeredModes(profile.data) : [...sessionModes];
  const [searchParams, setSearchParams] = useSearchParams();
  const mode = readMode(searchParams.get("mode"), allowed);
  const today = kolkataToday();
  const requestedDate = searchParams.get("date");
  const month = startOfMonth(isIsoDate(requestedDate) ? requestedDate : today);
  const range = monthQueryRange(month);
  const slotsQuery = useInstructorSlots(mode, range.from, range.to, profile.isSuccess);
  const bookingsQuery = useInstructorBookings(profile.isSuccess);
  const addSlots = useAddSlots();
  const updateSlot = useUpdateSlot();
  const blockSlot = useBlockSlot();
  const unblockSlot = useUnblockSlot();
  const deleteSlot = useDeleteSlot();
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState<OwnedSlot | null>(null);
  const [removing, setRemoving] = useState<OwnedSlot | null>(null);

  const occupied = useMemo(() => {
    const ids = new Set<string>();
    for (const booking of bookingsQuery.data ?? []) {
      if (bookingOccupiesSlot(booking.status)) ids.add(booking.slotId);
    }
    return ids;
  }, [bookingsQuery.data]);

  const slots = useMemo(
    () =>
      [...(slotsQuery.data?.slots ?? [])].sort((a, b) =>
        a.date === b.date ? a.start.localeCompare(b.start) : a.date.localeCompare(b.date),
      ),
    [slotsQuery.data?.slots],
  );

  const selectedDate = clampDateToMonth(month, isIsoDate(requestedDate) ? requestedDate : null, today);
  const daySlots = slots.filter((slot) => slot.date === selectedDate);
  const slotCounts = useMemo(() => countSlotsByDate(slots), [slots]);

  function setParam(key: string, value: string) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set(key, value);
    setSearchParams(nextParams, { replace: true });
  }

  if (profile.isLoading) return <PageLoader />;
  if (profile.isError) {
    return (
      <main className="mx-auto max-w-3xl px-4 py-8 sm:px-8">
        <ErrorState
          title="Couldn’t load your workspace"
          message={toUserMessage(profile.error)}
          onRetry={() => void profile.refetch()}
        />
      </main>
    );
  }

  if (!profile.data) return <PageLoader />;

  const canManage = canManageSlots(profile.data);
  const busyId =
    (blockSlot.isPending ? blockSlot.variables : undefined) ??
    (unblockSlot.isPending ? unblockSlot.variables : undefined) ??
    (deleteSlot.isPending ? deleteSlot.variables : undefined) ??
    (updateSlot.isPending ? updateSlot.variables?.id : undefined);

  return (
    <main className="mx-auto max-w-3xl space-y-5 px-4 py-6 sm:px-8">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <p className="text-[11px] font-medium tracking-[0.22em] text-brand-muted uppercase">Your month</p>
          <h1 className="font-heading text-2xl font-medium sm:text-3xl">Availability</h1>
          <p className="text-sm leading-relaxed text-brand-muted">
            {canManage
              ? "Pick a day, then add, edit, or delete times. Customers book in 12-hour time."
              : "You can review this month here. Add, edit, and delete stay locked until your profile is verified."}
          </p>
        </div>
        {canManage ? (
          <Button
            variant="contained"
            startIcon={<Plus className="size-4" />}
            onClick={() => setAdding(true)}
            sx={{ minHeight: 44, borderRadius: "14px", flexShrink: 0 }}
          >
            Add slot
          </Button>
        ) : null}
      </header>

      <StatusBanner profile={profile.data} />

      <ModeChips
        value={mode}
        options={allowed.length > 0 ? allowed : [...sessionModes]}
        onChange={(next) => setParam("mode", next)}
      />

      {slotsQuery.isLoading ? <SlotWeekSkeleton /> : null}
      {slotsQuery.isError ? (
        <ErrorState message={toUserMessage(slotsQuery.error)} onRetry={() => void slotsQuery.refetch()} />
      ) : null}

      {slotsQuery.isSuccess ? (
        <MonthCalendar
          month={month}
          selected={selectedDate}
          slotCounts={slotCounts}
          countNoun="slot"
          onSelect={(date) => setParam("date", date)}
          onMonthChange={(nextMonth) => setParam("date", clampDateToMonth(nextMonth, null, today))}
        />
      ) : null}

      {slotsQuery.isSuccess ? (
        <section className="space-y-3">
          {daySlots.length === 0 ? (
            <EmptyState
              title={`No ${mode.toLowerCase()} times on this day`}
              description={
                canManage
                  ? "Add a slot for this date, or pick another day."
                  : "Times can be added after an admin verifies your profile."
              }
              actionLabel={canManage ? "Add slot" : undefined}
              onAction={canManage ? () => setAdding(true) : undefined}
            />
          ) : (
            daySlots.map((slot) => (
              <SlotCard
                key={slot.id}
                slot={slot}
                state={slotState(slot, occupied.has(slot.id), today)}
                busy={busyId === slot.id}
                canManage={canManage}
                onEdit={setEditing}
                onDelete={setRemoving}
                onBlock={(id) => {
                  blockSlot.mutate(id, {
                    onSuccess: () => toast.success("Blocked. Customers no longer see this slot."),
                    onError: (error) => toast.error(toUserMessage(error)),
                  });
                }}
                onUnblock={(id) => {
                  unblockSlot.mutate(id, {
                    onSuccess: () => toast.success("Unblocked. Customers can book this slot again."),
                    onError: (error) => toast.error(toUserMessage(error)),
                  });
                }}
              />
            ))
          )}
        </section>
      ) : null}

      <Dialog
        open={adding && canManage}
        onClose={() => setAdding(false)}
        fullWidth
        maxWidth="sm"
        transitionDuration={0}
        slotProps={{
          backdrop: { sx: { bgcolor: "rgba(37, 49, 39, 0.45)" } },
          paper: {
            sx: {
              borderRadius: "24px",
              bgcolor: "#FFFFFF",
              backgroundImage: "none",
              opacity: 1,
            },
          },
        }}
      >
        <DialogContent sx={{ p: 3 }}>
          <SlotForm
            key={`${selectedDate}-add`}
            title={`Add ${mode === "Online" ? "an" : "a"} ${mode.toLowerCase()} slot`}
            description="Customers only see open times for this session type."
            submitLabel="Add slot"
            busy={addSlots.isPending}
            defaultValues={{ date: selectedDate < today ? today : selectedDate, start: "07:00", end: "08:00" }}
            onCancel={() => setAdding(false)}
            onSubmit={(values) => {
              addSlots.mutate(
                { mode, ...values },
                {
                  onSuccess: () => {
                    toast.success("Slot added.");
                    setAdding(false);
                    setParam("date", values.date);
                  },
                  onError: (error) => toast.error(toUserMessage(error)),
                },
              );
            }}
          />
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(editing) && canManage}
        onClose={() => setEditing(null)}
        fullWidth
        maxWidth="sm"
        transitionDuration={0}
        slotProps={{
          backdrop: { sx: { bgcolor: "rgba(37, 49, 39, 0.45)" } },
          paper: {
            sx: {
              borderRadius: "24px",
              bgcolor: "#FFFFFF",
              backgroundImage: "none",
              opacity: 1,
            },
          },
        }}
      >
        <DialogContent sx={{ p: 3 }}>
          {editing ? (
            <SlotForm
              key={editing.id}
              title="Edit slot"
              description="Change the date or time. Booked slots cannot be edited."
              submitLabel="Save changes"
              busy={updateSlot.isPending}
              defaultValues={{ date: editing.date, start: editing.start, end: editing.end }}
              onCancel={() => setEditing(null)}
              onSubmit={(values) => {
                updateSlot.mutate(
                  { id: editing.id, ...values },
                  {
                    onSuccess: () => {
                      toast.success("Slot updated.");
                      setEditing(null);
                      setParam("date", values.date);
                    },
                    onError: (error) => toast.error(toUserMessage(error)),
                  },
                );
              }}
            />
          ) : null}
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(removing) && canManage}
        onClose={() => {
          if (!deleteSlot.isPending) setRemoving(null);
        }}
        fullWidth
        maxWidth="xs"
        transitionDuration={0}
        slotProps={{
          backdrop: { sx: { bgcolor: "rgba(37, 49, 39, 0.45)" } },
          paper: {
            sx: {
              borderRadius: "24px",
              bgcolor: "#FFFFFF",
              backgroundImage: "none",
              opacity: 1,
            },
          },
        }}
      >
        <DialogTitle sx={{ fontFamily: "inherit", pb: 1 }}>Delete this slot?</DialogTitle>
        <DialogContent>
          {removing ? (
            <Typography variant="body2" color="text.secondary">
              {formatTimeRange12(removing.start, removing.end)} on {formatSlotDay(removing.date)} will be
              removed. Customers will no longer see this time.
            </Typography>
          ) : null}
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2.5 }}>
          <Button variant="text" disabled={deleteSlot.isPending} onClick={() => setRemoving(null)}>
            Cancel
          </Button>
          <Button
            color="error"
            variant="contained"
            disabled={!removing || deleteSlot.isPending}
            onClick={() => {
              if (!removing) return;
              deleteSlot.mutate(removing.id, {
                onSuccess: () => {
                  toast.success("Slot removed.");
                  setRemoving(null);
                },
                onError: (error) => toast.error(toUserMessage(error)),
              });
            }}
          >
            Delete
          </Button>
        </DialogActions>
      </Dialog>
    </main>
  );
}
