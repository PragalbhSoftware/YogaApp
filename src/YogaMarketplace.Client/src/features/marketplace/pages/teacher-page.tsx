import { Link, useNavigate, useParams } from "react-router-dom";
import { Button } from "@mui/material";
import { ArrowLeft } from "lucide-react";
import { routes } from "@/constants/routes";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { PageLoader } from "@/components/common/page-loader";
import { AvailabilitySection } from "@/features/marketplace/components/availability-section";
import { InstructorAbout } from "@/features/marketplace/components/instructor-about";
import { InstructorAvatar } from "@/features/marketplace/components/instructor-avatar";
import { InstructorCredentials } from "@/features/marketplace/components/instructor-credentials";
import { InstructorGallery } from "@/features/marketplace/components/instructor-gallery";
import { IntroVideo } from "@/features/marketplace/components/intro-video";
import { ModeRateList } from "@/features/marketplace/components/mode-rate-list";
import { ReviewList } from "@/features/marketplace/components/review-list";
import { TrialOfferNote } from "@/features/marketplace/components/trial-offer-note";
import { VerifiedBadge } from "@/features/marketplace/components/verified-badge";
import { useInstructor, useInstructorReviews } from "@/features/marketplace/hooks/use-instructors";
import { areaClosedDescription, areaClosedTitle } from "@/features/marketplace/utils/areas";
import { usePageTitle } from "@/hooks/use-page-title";
import { toUserMessage } from "@/services/http/api-error";

export function TeacherPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const instructor = useInstructor(id);
  const reviews = useInstructorReviews(id);
  usePageTitle(instructor.data?.displayName ?? "Instructor");

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
            <InstructorAvatar
              name={instructor.data.displayName}
              photoUrl={instructor.data.photoUrls[0]}
              size="lg"
            />
            <div className="space-y-1">
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="font-heading text-2xl font-medium">{instructor.data.displayName}</h1>
                {instructor.data.isVerified ? <VerifiedBadge /> : null}
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

          <InstructorGallery name={instructor.data.displayName} photoUrls={instructor.data.photoUrls} />

          <InstructorAbout instructor={instructor.data} />

          <IntroVideo name={instructor.data.displayName} videoId={instructor.data.introVideoId} />

          <InstructorCredentials certificates={instructor.data.certificates} />

          <ModeRateList modes={instructor.data.modes ?? []} heading="Session fees" />

          <ReviewList
            reviews={reviews.data}
            isLoading={reviews.isLoading}
            isError={reviews.isError}
            error={reviews.error}
            onRetry={() => void reviews.refetch()}
          />

          {instructor.data.areaOpen && instructor.data.trial ? (
            <TrialOfferNote name={instructor.data.displayName} trial={instructor.data.trial} />
          ) : null}

          {instructor.data.areaOpen ? (
            <AvailabilitySection instructorId={instructor.data.id} modes={instructor.data.modes ?? []} />
          ) : (
            <EmptyState
              title={areaClosedTitle}
              description={areaClosedDescription}
              actionLabel="Browse instructors"
              onAction={() => navigate(routes.home)}
            />
          )}
        </article>
      ) : null}
    </main>
  );
}
