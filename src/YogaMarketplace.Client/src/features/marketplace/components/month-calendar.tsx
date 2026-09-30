import { ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@/utils/cn";
import {
  addMonths,
  calendarWeekdays,
  endOfMonth,
  formatCalendarDay,
  formatCalendarMonth,
  formatSlotDay,
  kolkataToday,
  monthGrid,
  startOfMonth,
} from "@/utils/clock";

type MonthCalendarProps = {
  month: string;
  selected: string;
  slotCounts: ReadonlyMap<string, number>;
  onSelect: (date: string) => void;
  onMonthChange: (month: string) => void;
  minDate?: string;
  countNoun?: "time" | "slot";
};

export function countSlotsByDate(slots: readonly { date: string }[]) {
  const counts = new Map<string, number>();
  for (const slot of slots) {
    counts.set(slot.date, (counts.get(slot.date) ?? 0) + 1);
  }
  return counts;
}

export function MonthCalendar({
  month,
  selected,
  slotCounts,
  onSelect,
  onMonthChange,
  minDate,
  countNoun = "time",
}: MonthCalendarProps) {
  const today = kolkataToday();
  const cells = monthGrid(month);
  const previousMonth = addMonths(month, -1);
  const nextMonth = addMonths(month, 1);
  const canGoPrevious = !minDate || endOfMonth(previousMonth) >= minDate;
  const selectedCount = slotCounts.get(selected) ?? 0;
  const selectedNoun = selectedCount === 1 ? countNoun : `${countNoun}s`;

  return (
    <div className="overflow-hidden rounded-[28px] border border-brand-border bg-brand-surface shadow-[0_8px_24px_rgba(37,49,39,0.05)]">
      <div className="px-3 pb-2 pt-4 sm:px-4">
        <div className="mb-3 flex items-center justify-between gap-2">
          <button
            type="button"
            aria-label="Previous month"
            disabled={!canGoPrevious}
            onClick={() => onMonthChange(startOfMonth(previousMonth))}
            className="inline-flex size-11 items-center justify-center rounded-full text-brand-text hover:bg-brand-background disabled:cursor-not-allowed disabled:text-brand-border"
          >
            <ChevronLeft className="size-5" aria-hidden="true" />
          </button>
          <p className="font-heading text-lg font-medium">{formatCalendarMonth(month)}</p>
          <button
            type="button"
            aria-label="Next month"
            onClick={() => onMonthChange(startOfMonth(nextMonth))}
            className="inline-flex size-11 items-center justify-center rounded-full text-brand-text hover:bg-brand-background"
          >
            <ChevronRight className="size-5" aria-hidden="true" />
          </button>
        </div>

        <div className="mb-1 grid grid-cols-7" aria-hidden="true">
          {calendarWeekdays.map((day) => (
            <p
              key={day}
              className="py-1 text-center text-[10px] font-semibold tracking-[0.16em] text-brand-muted uppercase"
            >
              {day}
            </p>
          ))}
        </div>

        <div className="grid grid-cols-7 gap-0.5" role="listbox" aria-label="Choose a date">
          {cells.map((cell) => {
            const active = cell.date === selected;
            const isToday = cell.date === today;
            const count = slotCounts.get(cell.date) ?? 0;
            const disabled = Boolean(minDate && cell.date < minDate);
            const noun = count === 1 ? countNoun : `${countNoun}s`;
            const label = [
              formatSlotDay(cell.date),
              isToday ? "today" : null,
              disabled ? "unavailable" : null,
              count === 0 ? `no ${countNoun}s` : `${count} ${noun}`,
            ]
              .filter(Boolean)
              .join(", ");

            return (
              <button
                key={cell.date}
                type="button"
                role="option"
                aria-label={label}
                aria-selected={active}
                aria-current={isToday ? "date" : undefined}
                aria-disabled={disabled || undefined}
                disabled={disabled}
                onClick={() => onSelect(cell.date)}
                className={cn(
                  "flex min-h-[3.75rem] flex-col items-center justify-center gap-0.5 rounded-2xl px-0.5 py-1.5 sm:min-h-[4.25rem]",
                  "transition-colors duration-150",
                  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-primary focus-visible:ring-offset-2 focus-visible:ring-offset-brand-surface",
                  active && "bg-brand-primary text-white",
                  !active && !disabled && isToday && "bg-[rgba(101,122,91,0.08)]",
                  !active && !disabled && !isToday && "hover:bg-brand-background",
                  disabled && "cursor-not-allowed opacity-35",
                  !active && !cell.inMonth && !disabled && "opacity-45",
                )}
              >
                <span
                  className={cn(
                    "font-heading text-base font-medium tabular-nums leading-none sm:text-lg",
                    active && "text-white",
                    !active && cell.inMonth && "text-brand-text",
                    !active && !cell.inMonth && "text-brand-muted",
                  )}
                >
                  {formatCalendarDay(cell.date)}
                </span>
                {count > 0 && !disabled ? (
                  <span
                    className={cn(
                      "text-[10px] font-semibold tabular-nums leading-none",
                      active ? "text-white/85" : "text-brand-primary",
                    )}
                  >
                    {count}
                  </span>
                ) : (
                  <span className="h-2.5" aria-hidden="true" />
                )}
              </button>
            );
          })}
        </div>
      </div>

      <div className="flex items-end justify-between gap-3 border-t border-brand-border bg-brand-background/70 px-5 py-4">
        <div className="min-w-0">
          <p className="text-[11px] font-medium tracking-[0.18em] text-brand-muted uppercase">Selected day</p>
          <p className="font-heading text-lg font-medium leading-snug">{formatSlotDay(selected)}</p>
        </div>
        <p className="pb-0.5 text-sm font-medium text-brand-primary">
          {selectedCount === 0 ? `No ${countNoun}s` : `${selectedCount} ${selectedNoun}`}
        </p>
      </div>
    </div>
  );
}
