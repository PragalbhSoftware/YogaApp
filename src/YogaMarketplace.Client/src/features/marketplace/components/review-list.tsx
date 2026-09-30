import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import type { PublicReview } from "@/features/marketplace/types";
import { toUserMessage } from "@/services/http/api-error";
import { formatWhenKolkata } from "@/utils/clock";

type ReviewListProps = {
  reviews: PublicReview[] | undefined;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  onRetry: () => void;
};

export function ReviewList({ reviews, isLoading, isError, error, onRetry }: ReviewListProps) {
  return (
    <section className="space-y-3">
      <h2 className="font-heading text-lg font-medium">Reviews</h2>
      {isLoading ? <BookingListSkeleton /> : null}
      {isError ? <ErrorState message={toUserMessage(error)} onRetry={onRetry} /> : null}
      {reviews && reviews.length === 0 ? (
        <EmptyState
          title="No reviews yet"
          description="Students can leave a rating after a completed session."
        />
      ) : null}
      {reviews && reviews.length > 0 ? (
        <ul className="space-y-3">
          {reviews.map((review, index) => (
            <li
              key={`${review.createdAt}-${review.reviewerName}-${index}`}
              className="rounded-3xl border border-brand-border bg-brand-surface p-5"
            >
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <p className="font-medium">
                  {review.rating}/5 · {review.reviewerName}
                </p>
                <p className="text-xs text-brand-muted">{formatWhenKolkata(review.createdAt)}</p>
              </div>
              {review.comment ? (
                <p className="mt-2 text-sm leading-relaxed text-brand-text">{review.comment}</p>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
