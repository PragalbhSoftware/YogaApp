import { useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import type { SessionMode } from "@/constants/catalog";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { SlotWeekSkeleton } from "@/components/common/loading-skeleton";
import { ModeChips } from "@/features/instructor/components/mode-chips";
import { OpenSlotRow } from "@/features/marketplace/components/open-slot-row";
import { countSlotsByDate, MonthCalendar } from "@/features/marketplace/components/month-calendar";
import { useInstructorOpenSlots } from "@/features/marketplace/hooks/use-instructors";
import type { ModeRate, OpenSlot } from "@/features/marketplace/types";
import { offeredSessionModes } from "@/features/marketplace/utils/modes";
import { toUserMessage } from "@/services/http/api-error";
import {
  clampDateToMonth,
  enumerateIsoDates,
  isIsoDate,
  kolkataToday,
  monthQueryRange,
  startOfMonth,
  slotHasEnded,
} from "@/utils/clock";

type AvailabilitySectionProps = {
  instructorId: string;
  modes: ModeRate[];
};

function readMode(value: string | null, allowed: SessionMode[]): SessionMode {
  if (value && allowed.includes(value as SessionMode)) return value as SessionMode;
  return allowed[0] ?? "Home";
}

function pickDate(
  requested: string | null,
  windowDates: string[],
  slots: OpenSlot[],
  month: string,
  minDate?: string,
) {
  if (requested && windowDates.includes(requested) && (!minDate || requested >= minDate)) {
    return requested;
  }

  const monthStart = startOfMonth(month);
  const bookableDates = [
    ...new Set(slots.filter((slot) => !slotHasEnded(slot.date, slot.end)).map((slot) => slot.date)),
  ].filter((date) => !minDate || date >= minDate);
  const inMonth = bookableDates.filter((date) => date.startsWith(monthStart.slice(0, 7)));
  if (inMonth[0]) return inMonth[0];
  if (bookableDates[0]) return bookableDates[0];
  if (minDate && windowDates.includes(minDate)) return minDate;
  const today = kolkataToday();
  if (windowDates.includes(today)) return today;
  return windowDates[0] ?? today;
}

export function AvailabilitySection({ instructorId, modes }: AvailabilitySectionProps) {
  const allowed = offeredSessionModes(modes);
  const [searchParams, setSearchParams] = useSearchParams();
  const mode = readMode(searchParams.get("mode"), allowed);
  const today = kolkataToday();
  const requestedDate = searchParams.get("date");
  const month = startOfMonth(isIsoDate(requestedDate) ? requestedDate : today);
  const range = monthQueryRange(month);
  const slotsQuery = useInstructorOpenSlots(instructorId, mode, range.from, range.to, allowed.length > 0);

  const windowDates = enumerateIsoDates(range.from, range.to);

  const slots = useMemo(
    () =>
      [...(slotsQuery.data?.slots ?? [])].sort((a, b) =>
        a.date === b.date ? a.start.localeCompare(b.start) : a.date.localeCompare(b.date),
      ),
    [slotsQuery.data?.slots],
  );

  const selectedDate = pickDate(requestedDate, windowDates, slots, month, today);
  const daySlots = slots.filter((slot) => slot.date === selectedDate);
  const slotCounts = useMemo(() => countSlotsByDate(slots), [slots]);

  function setParam(key: string, value: string) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set(key, value);
    setSearchParams(nextParams, { replace: true });
  }

  if (allowed.length === 0) {
    return (
      <section className="space-y-3">
        <h2 className="font-heading text-lg font-medium">Open slots</h2>
        <EmptyState
          title="No session types yet"
          description="This instructor has not listed Home, Studio, or Online times."
        />
      </section>
    );
  }

  return (
    <section className="space-y-4" aria-labelledby="open-slots-heading">
      <div className="space-y-1">
        <h2 id="open-slots-heading" className="font-heading text-lg font-medium">
          Open slots
        </h2>
        <p className="text-sm leading-relaxed text-brand-muted">
          Pick a date, then a time. Times are India Standard Time.
        </p>
      </div>

      <ModeChips value={mode} options={allowed} onChange={(next) => setParam("mode", next)} />

      {slotsQuery.isLoading ? <SlotWeekSkeleton /> : null}
      {slotsQuery.isError ? (
        <ErrorState message={toUserMessage(slotsQuery.error)} onRetry={() => void slotsQuery.refetch()} />
      ) : null}

      {slotsQuery.isSuccess ? (
        <MonthCalendar
          month={month}
          selected={selectedDate}
          slotCounts={slotCounts}
          minDate={today}
          onSelect={(date) => setParam("date", date)}
          onMonthChange={(nextMonth) => setParam("date", clampDateToMonth(nextMonth, null, today))}
        />
      ) : null}

      {slotsQuery.isSuccess ? (
        <div className="space-y-3">
          {daySlots.length === 0 ? (
            <EmptyState
              title={`No ${mode.toLowerCase()} times on this day`}
              description="Choose another date, or try a different session type."
            />
          ) : (
            daySlots.map((slot) => (
              <OpenSlotRow key={slot.id} slot={slot} providerId={instructorId} />
            ))
          )}
        </div>
      ) : null}
    </section>
  );
}
