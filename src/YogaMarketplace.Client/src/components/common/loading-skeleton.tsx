import { Skeleton } from "@mui/material";

export function InstructorListSkeleton() {
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      {Array.from({ length: 4 }, (_, index) => (
        <div
          key={index}
          className="rounded-3xl bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.05)]"
        >
          <div className="flex gap-4">
            <Skeleton variant="circular" width={56} height={56} />
            <div className="min-w-0 flex-1">
              <Skeleton width="60%" height={28} />
              <Skeleton width="40%" height={20} />
            </div>
          </div>
          <Skeleton className="mt-4" height={36} />
          <Skeleton className="mt-3" height={44} />
        </div>
      ))}
    </div>
  );
}

export function BookingListSkeleton() {
  return (
    <div className="space-y-4" aria-hidden="true">
      {Array.from({ length: 3 }, (_, index) => (
        <Skeleton key={index} variant="rounded" height={220} sx={{ borderRadius: 4 }} />
      ))}
    </div>
  );
}

export function AreaPickerSkeleton() {
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
      {Array.from({ length: 6 }, (_, index) => (
        <Skeleton key={index} variant="rounded" height={56} sx={{ borderRadius: 3 }} />
      ))}
    </div>
  );
}

export function SlotWeekSkeleton() {
  return (
    <div className="space-y-4" aria-hidden="true">
      <Skeleton variant="rounded" height={360} sx={{ borderRadius: "28px" }} />
      {Array.from({ length: 2 }, (_, index) => (
        <Skeleton key={index} variant="rounded" height={132} sx={{ borderRadius: 6 }} />
      ))}
    </div>
  );
}

export function AddressCardSkeleton() {
  return (
    <div className="space-y-5 rounded-3xl border border-brand-border bg-brand-surface p-5 sm:p-6" aria-hidden="true">
      <div className="flex gap-3">
        <Skeleton variant="rounded" width={44} height={44} sx={{ borderRadius: 4 }} />
        <div className="min-w-0 flex-1">
          <Skeleton width="45%" height={28} />
          <Skeleton width="75%" height={18} />
        </div>
      </div>
      <div className="space-y-5">
        <Skeleton variant="rounded" height={72} sx={{ borderRadius: 4 }} />
        <Skeleton variant="rounded" height={52} sx={{ borderRadius: 4 }} />
        <div className="grid gap-5 sm:grid-cols-2">
          <Skeleton variant="rounded" height={52} sx={{ borderRadius: 4 }} />
          <Skeleton variant="rounded" height={52} sx={{ borderRadius: 4 }} />
        </div>
        <Skeleton variant="rounded" height={52} sx={{ borderRadius: 4 }} />
      </div>
    </div>
  );
}
