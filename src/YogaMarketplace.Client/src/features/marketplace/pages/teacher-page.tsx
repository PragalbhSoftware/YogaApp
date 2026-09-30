import { Link, useParams } from "react-router-dom";
import { Button } from "@mui/material";
import { ArrowLeft, ShieldCheck } from "lucide-react";
import { routes } from "@/constants/routes";
import { ErrorState } from "@/components/common/error-state";
import { PageLoader } from "@/components/common/page-loader";
import { AvailabilitySection } from "@/features/marketplace/components/availability-section";
import { InstructorAbout } from "@/features/marketplace/components/instructor-about";
import { ModeRateList } from "@/features/marketplace/components/mode-rate-list";
import { ReviewList } from "@/features/marketplace/components/review-list";
import { useInstructor, useInstructorReviews } from "@/features/marketplace/hooks/use-instructors";
import { toUserMessage } from "@/services/http/api-error";
import { initials } from "@/utils/initials";

export function TeacherPage() {
  const { id } = useParams();
  const instructor = useInstructor(id);
  const reviews = useInstructorReviews(id);

  return (
    <main className="mx-auto max-w-3xl px-4 py-6 sm:px-8">
      <Button
        component={Link}
        to={routes.home}
        startIcon={<ArrowLeft className="size-4" />}
        variant="text"
        sx={{ mb: 2, px: 0 }}
      >
        Back to instructors
      </Button>

      {instructor.isLoading ? <PageLoader /> : null}
      {instructor.isError ? (
        <ErrorState
          title="Instructor unavailable"
          message={toUserMessage(instructor.error)}
          onRetry={() => void instructor.refetch()}
        />
      ) : null}
      {instructor.data ? (
        <article className="space-y-6">
          <header className="flex gap-4 rounded-3xl bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.06)]">
            <div
              className="flex size-16 shrink-0 items-center justify-center rounded-full bg-brand-primary/10 font-heading text-xl text-brand-primary"
              aria-hidden="true"
            >
              {initials(instructor.data.displayName)}
            </div>
            <div className="space-y-1">
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="font-heading text-2xl font-medium">{instructor.data.displayName}</h1>
                {instructor.data.status.toLowerCase() === "verified" ? (
                  <span className="inline-flex items-center gap-1 text-xs font-medium text-brand-primary">
                    <ShieldCheck className="size-3.5" aria-hidden="true" />
                    Verified
                  </span>
                ) : null}
              </div>
              <p className="text-sm text-brand-muted">{instructor.data.area}</p>
              {instructor.data.reviewCount > 0 && instructor.data.ratingAverage != null ? (
                <p className="text-sm">
                  {instructor.data.ratingAverage.toFixed(1)} · {instructor.data.reviewCount}{" "}
                  {instructor.data.reviewCount === 1 ? "review" : "reviews"}
                </p>
              ) : null}
            </div>
          </header>

          <InstructorAbout instructor={instructor.data} />

          <ModeRateList modes={instructor.data.modes ?? []} heading="Session fees" />

          <ReviewList
            reviews={reviews.data}
            isLoading={reviews.isLoading}
            isError={reviews.isError}
            error={reviews.error}
            onRetry={() => void reviews.refetch()}
          />

          <AvailabilitySection instructorId={instructor.data.id} modes={instructor.data.modes ?? []} />
        </article>
      ) : null}
    </main>
  );
}
