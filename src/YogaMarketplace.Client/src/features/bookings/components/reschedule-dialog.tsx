import { useEffect, useMemo, useState } from "react";
import { Button, Dialog, DialogContent } from "@mui/material";
import { Clock } from "lucide-react";
import { sessionModes, type SessionMode } from "@/constants/catalog";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { SlotWeekSkeleton } from "@/components/common/loading-skeleton";
import { countSlotsByDate, MonthCalendar } from "@/features/marketplace/components/month-calendar";
import { useInstructorOpenSlots } from "@/features/marketplace/hooks/use-instructors";
import type { OpenSlot } from "@/features/marketplace/types";
import type { BookingRecord } from "@/features/bookings/types";
import { toUserMessage } from "@/services/http/api-error";
import {
  clampDateToMonth,
  durationLabel,
  enumerateIsoDates,
  formatTime12,
  kolkataToday,
  monthQueryRange,
  startOfMonth,
  slotHasEnded,
} from "@/utils/clock";

type RescheduleDialogProps = {
  booking: BookingRecord | null;
  busy: boolean;
  onClose: () => void;
  onConfirm: (slotId: string) => void;
};

function isSessionMode(value: string): value is SessionMode {
  return sessionModes.includes(value as SessionMode);
}

function pickDate(
  requested: string | null,
  windowDates: string[],
  slots: OpenSlot[],
  month: string,
  minDate: string,
) {
  if (requested && windowDates.includes(requested) && requested >= minDate) {
    return requested;
  }

  const monthStart = startOfMonth(month);
  const bookableDates = [
    ...new Set(slots.filter((slot) => !slotHasEnded(slot.date, slot.end)).map((slot) => slot.date)),
  ].filter((date) => date >= minDate);
  const inMonth = bookableDates.filter((date) => date.startsWith(monthStart.slice(0, 7)));
  if (inMonth[0]) return inMonth[0];
  if (bookableDates[0]) return bookableDates[0];
  if (windowDates.includes(minDate)) return minDate;
  return windowDates[0] ?? minDate;
}

export function RescheduleDialog({ booking, busy, onClose, onConfirm }: RescheduleDialogProps) {
  const mode = booking && isSessionMode(booking.mode) ? booking.mode : "Home";
  const today = kolkataToday();
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const month = startOfMonth(selectedDate ?? today);
  const range = monthQueryRange(month);
  const slotsQuery = useInstructorOpenSlots(booking?.providerId, mode, range.from, range.to, Boolean(booking));

  useEffect(() => {
    setSelectedDate(null);
  }, [booking?.id]);

  const windowDates = enumerateIsoDates(range.from, range.to);

  const slots = useMemo(
    () =>
      [...(slotsQuery.data?.slots ?? [])]
        .filter((slot) => slot.id !== booking?.slotId)
        .filter((slot) => !slotHasEnded(slot.date, slot.end))
        .sort((a, b) => (a.date === b.date ? a.start.localeCompare(b.start) : a.date.localeCompare(b.date))),
    [booking?.slotId, slotsQuery.data?.slots],
  );

  const date = pickDate(selectedDate, windowDates, slots, month, today);
  const daySlots = slots.filter((slot) => slot.date === date);
  const slotCounts = useMemo(() => countSlotsByDate(slots), [slots]);

  return (
    <Dialog
      open={Boolean(booking)}
      onClose={() => {
        if (!busy) onClose();
      }}
      fullWidth
      maxWidth="md"
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
        <div className="space-y-4">
          <div>
            <h2 className="font-heading text-lg font-medium">Reschedule</h2>
            <p className="mt-1 text-sm leading-relaxed text-brand-muted">
              Pick another open {mode.toLowerCase()} time with {booking?.providerName}. Payment stays paid.
            </p>
          </div>

          {slotsQuery.isLoading ? <SlotWeekSkeleton /> : null}
          {slotsQuery.isError ? (
            <ErrorState message={toUserMessage(slotsQuery.error)} onRetry={() => void slotsQuery.refetch()} />
          ) : null}

          {slotsQuery.isSuccess ? (
            <MonthCalendar
              month={month}
              selected={date}
              slotCounts={slotCounts}
              minDate={today}
              onSelect={setSelectedDate}
              onMonthChange={(nextMonth) => setSelectedDate(clampDateToMonth(nextMonth, null, today))}
            />
          ) : null}

          {slotsQuery.isSuccess ? (
            <div className="space-y-3">
              {daySlots.length === 0 ? (
                <EmptyState
                  title={slots.length === 0 ? "No other open times" : `No ${mode.toLowerCase()} times on this day`}
                  description={
                    slots.length === 0
                      ? "This instructor has no other open slots for this session type."
                      : "Choose another date."
                  }
                />
              ) : (
                daySlots.map((slot) => {
                  const duration = durationLabel(slot.start, slot.end);
                  return (
                    <article
                      key={slot.id}
                      className="flex items-center gap-4 rounded-[24px] border border-brand-border bg-brand-surface px-4 py-4"
                    >
                      <span
                        className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-[rgba(101,122,91,0.12)] text-brand-primary"
                        aria-hidden="true"
                      >
                        <Clock className="size-5" />
                      </span>
                      <div className="min-w-0 flex-1">
                        <p className="font-heading text-xl font-medium tracking-tight">
                          {formatTime12(slot.start)}
                        </p>
                        <p className="mt-0.5 text-sm text-brand-muted">
                          to {formatTime12(slot.end)}
                          {duration ? ` · ${duration}` : ""}
                        </p>
                      </div>
                      <Button
                        variant="contained"
                        size="small"
                        disabled={busy}
                        onClick={() => onConfirm(slot.id)}
                        sx={{ flexShrink: 0, minHeight: 44, borderRadius: "16px", px: 2.5 }}
                      >
                        {busy ? "Moving…" : "Move here"}
                      </Button>
                    </article>
                  );
                })
              )}
            </div>
          ) : null}

          <Button type="button" variant="text" fullWidth disabled={busy} onClick={onClose}>
            Cancel
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
