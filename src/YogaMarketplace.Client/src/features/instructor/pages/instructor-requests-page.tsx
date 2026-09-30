import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { StatusFilter } from "@/features/bookings/components/status-filter";
import { isBookingStatus } from "@/features/bookings/types";
import { RequestCard } from "@/features/instructor/components/request-card";
import {
  useAcceptBooking,
  useCompleteBooking,
  useDeclineBooking,
  useInstructorRequests,
  useMarkNoShow,
} from "@/features/instructor/hooks/use-instructor-schedule";
import { toUserMessage } from "@/services/http/api-error";

function readStatus(value: string | null) {
  return isBookingStatus(value) ? value : undefined;
}

export function InstructorRequestsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const status = readStatus(searchParams.get("status"));
  const requests = useInstructorRequests(status);
  const accept = useAcceptBooking();
  const decline = useDeclineBooking();
  const complete = useCompleteBooking();
  const noShow = useMarkNoShow();
  const pendingId =
    (accept.isPending ? accept.variables : undefined) ??
    (decline.isPending ? decline.variables : undefined) ??
    (complete.isPending ? complete.variables : undefined) ??
    (noShow.isPending ? noShow.variables : undefined);

  function setStatus(next: string | null) {
    const params = new URLSearchParams(searchParams);
    if (next) params.set("status", next);
    else params.delete("status");
    setSearchParams(params, { replace: true });
  }

  async function run(
    action: (id: string) => Promise<unknown>,
    id: string,
    success: string,
  ) {
    try {
      await action(id);
      toast.success(success);
    } catch (error) {
      toast.error(toUserMessage(error));
    }
  }

  return (
    <main className="mx-auto max-w-3xl space-y-6 px-4 py-8 sm:px-8">
      <header className="space-y-1">
        <p className="text-[11px] font-medium tracking-[0.22em] text-brand-muted uppercase">Requests</p>
        <h1 className="font-heading text-2xl font-medium">Session requests</h1>
        <p className="text-sm leading-relaxed text-brand-muted">
          Accept or decline a new request. Mark an upcoming session complete or no-show when it is done.
        </p>
      </header>

      <StatusFilter value={status ?? null} onChange={setStatus} />

      {requests.isLoading ? <BookingListSkeleton /> : null}
      {requests.isError ? (
        <ErrorState message={toUserMessage(requests.error)} onRetry={() => void requests.refetch()} />
      ) : null}

      {requests.isSuccess && requests.data.length === 0 ? (
        <EmptyState
          title="No sessions in this list"
          description="Paid requests show up here. Try another status if you are looking for an older session."
        />
      ) : null}

      {requests.isSuccess && requests.data.length > 0 ? (
        <div className="space-y-4">
          {requests.data.map((booking) => (
            <RequestCard
              key={booking.id}
              booking={booking}
              busy={pendingId === booking.id}
              onAccept={(id) =>
                void run(accept.mutateAsync, id, "Accepted. The session is upcoming.")
              }
              onDecline={(id) =>
                void run(
                  decline.mutateAsync,
                  id,
                  "Declined. The payment was refunded and the slot is free.",
                )
              }
              onComplete={(id) =>
                void run(complete.mutateAsync, id, "Marked complete. The customer can leave a review.")
              }
              onNoShow={(id) =>
                void run(
                  noShow.mutateAsync,
                  id,
                  "Marked no-show. Your payout is waiting for the owner.",
                )
              }
            />
          ))}
        </div>
      ) : null}
    </main>
  );
}
