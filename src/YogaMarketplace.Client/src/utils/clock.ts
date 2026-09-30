export function kolkataToday() {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Kolkata" }).format(new Date());
}

export function formatSlotDay(date: string) {
  const value = new Date(`${date}T12:00:00+05:30`);
  return new Intl.DateTimeFormat("en-IN", {
    weekday: "long",
    day: "numeric",
    month: "short",
  }).format(value);
}

export function formatDateRange(from: string, to: string) {
  const start = new Date(`${from}T12:00:00+05:30`);
  const end = new Date(`${to}T12:00:00+05:30`);
  const opts: Intl.DateTimeFormatOptions = { day: "numeric", month: "short", year: "numeric" };
  return `${new Intl.DateTimeFormat("en-IN", opts).format(start)} – ${new Intl.DateTimeFormat("en-IN", opts).format(end)}`;
}

export function formatWhenKolkata(value: string) {
  return new Intl.DateTimeFormat("en-IN", {
    timeZone: "Asia/Kolkata",
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(value));
}

export function toHHmm(value: string) {
  return value.slice(0, 5);
}

export function formatTime12(value: string) {
  const time = toHHmm(value);
  const match = /^(\d{2}):(\d{2})$/.exec(time);
  if (!match) return time;
  const hour24 = Number(match[1]);
  const minute = match[2];
  const period = hour24 >= 12 ? "PM" : "AM";
  const hour12 = hour24 % 12 === 0 ? 12 : hour24 % 12;
  return `${hour12}:${minute} ${period}`;
}

export function formatTimeRange12(start: string, end: string) {
  return `${formatTime12(start)} – ${formatTime12(end)}`;
}

export function durationLabel(start: string, end: string) {
  const from = toMinutes(start);
  const to = toMinutes(end);
  if (from == null || to == null || to <= from) return null;
  const minutes = to - from;
  if (minutes % 60 === 0) {
    const hours = minutes / 60;
    return hours === 1 ? "1 hour" : `${hours} hours`;
  }
  if (minutes < 60) return `${minutes} min`;
  return `${Math.floor(minutes / 60)} hr ${minutes % 60} min`;
}

function toMinutes(value: string) {
  const match = /^(\d{2}):(\d{2})$/.exec(toHHmm(value));
  if (!match) return null;
  return Number(match[1]) * 60 + Number(match[2]);
}

export function timeSelectOptions(extraValues: string[] = []) {
  const values = new Set<string>();
  for (let hour = 5; hour <= 22; hour += 1) {
    for (const minute of [0, 15, 30, 45]) {
      if (hour === 22 && minute > 0) break;
      values.add(`${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`);
    }
  }
  for (const extra of extraValues) {
    const time = toHHmm(extra);
    if (/^\d{2}:\d{2}$/.test(time)) values.add(time);
  }
  return [...values]
    .sort()
    .map((value) => ({ value, label: formatTime12(value) }));
}

export function slotHasEnded(date: string, end: string) {
  return isAtOrAfterKolkata(date, end);
}

export function slotHasStarted(date: string, start: string) {
  return isAtOrAfterKolkata(date, start);
}

function isAtOrAfterKolkata(date: string, clock: string) {
  const time = toHHmm(clock);
  if (!/^\d{2}:\d{2}$/.test(time)) return false;
  return Date.now() >= new Date(`${date}T${time}:00+05:30`).getTime();
}

export function addIsoDays(date: string, days: number) {
  const [year, month, day] = date.split("-").map(Number);
  const next = new Date(Date.UTC(year, month - 1, day + days));
  return next.toISOString().slice(0, 10);
}

export function enumerateIsoDates(from: string, to: string) {
  const dates: string[] = [];
  for (let current = from; current <= to; current = addIsoDays(current, 1)) {
    dates.push(current);
  }
  return dates;
}

export function formatCalendarWeekday(date: string) {
  return new Intl.DateTimeFormat("en-IN", {
    weekday: "short",
    timeZone: "Asia/Kolkata",
  }).format(new Date(`${date}T12:00:00+05:30`));
}

export function formatCalendarDay(date: string) {
  return new Intl.DateTimeFormat("en-IN", {
    day: "numeric",
    timeZone: "Asia/Kolkata",
  }).format(new Date(`${date}T12:00:00+05:30`));
}

export function formatCalendarSpan(from: string, to: string) {
  const start = new Date(`${from}T12:00:00+05:30`);
  const end = new Date(`${to}T12:00:00+05:30`);
  const opts: Intl.DateTimeFormatOptions = { day: "numeric", month: "short" };
  return `${new Intl.DateTimeFormat("en-IN", opts).format(start)} – ${new Intl.DateTimeFormat("en-IN", opts).format(end)}`;
}

export function isIsoDate(value: string | null | undefined): value is string {
  return Boolean(value && /^\d{4}-\d{2}-\d{2}$/.test(value));
}

export function startOfMonth(date: string) {
  return `${date.slice(0, 7)}-01`;
}

export function endOfMonth(date: string) {
  const [year, month] = date.split("-").map(Number);
  return new Date(Date.UTC(year, month, 0)).toISOString().slice(0, 10);
}

export function addMonths(date: string, delta: number) {
  const [year, month] = date.split("-").map(Number);
  return new Date(Date.UTC(year, month - 1 + delta, 1)).toISOString().slice(0, 10);
}

export function formatCalendarMonth(date: string) {
  return new Intl.DateTimeFormat("en-IN", {
    month: "long",
    year: "numeric",
    timeZone: "Asia/Kolkata",
  }).format(new Date(`${startOfMonth(date)}T12:00:00+05:30`));
}

export const calendarWeekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"] as const;

export type CalendarCell = {
  date: string;
  inMonth: boolean;
};

export function monthGrid(monthDate: string): CalendarCell[] {
  const start = startOfMonth(monthDate);
  const end = endOfMonth(monthDate);
  const sundayIndex = new Date(`${start}T12:00:00+05:30`).getDay();
  const mondayIndex = sundayIndex === 0 ? 6 : sundayIndex - 1;
  const first = addIsoDays(start, -mondayIndex);
  const cells: CalendarCell[] = [];
  for (let index = 0; index < 42; index += 1) {
    const date = addIsoDays(first, index);
    cells.push({ date, inMonth: date >= start && date <= end });
  }
  if (cells.slice(35).every((cell) => !cell.inMonth)) return cells.slice(0, 35);
  return cells;
}

export function monthQueryRange(monthDate: string) {
  const cells = monthGrid(monthDate);
  return { from: cells[0]?.date ?? startOfMonth(monthDate), to: cells.at(-1)?.date ?? endOfMonth(monthDate) };
}

export function clampDateToMonth(month: string, preferred: string | null, today: string) {
  const start = startOfMonth(month);
  const end = endOfMonth(month);
  if (preferred && preferred >= start && preferred <= end) return preferred;
  if (today >= start && today <= end) return today;
  return start;
}
