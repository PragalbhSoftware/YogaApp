import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Button, Chip } from "@mui/material";
import { ArrowLeft, CalendarDays } from "lucide-react";
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
import { ApiError, toUserMessage } from "@/services/http/api-error";

const BOOKING_ID = "book";

function isNotFound(error: unknown) {
  return error instanceof ApiError && error.status === 404;
}

export function TeacherPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const remoteHint = searchParams.get("remote") === "1";
  const instructor = useInstructor(id);
  const reviews = useInstructorReviews(id);
  usePageTitle(instructor.data?.displayName ?? "Instructor");
  const offered = instructor.data?.modes ?? [];
  const onlineOnlyFromData = offered.length > 0 && offered.every((item) => item.mode === "Online");
  const remoteOnly = remoteHint || onlineOnlyFromData;
  const modes = remoteOnly ? offered.filter((item) => item.mode === "Online") : offered;

  return (
    <main className="mx-auto max-w-5xl px-4 py-6 sm:px-8">
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
      {instructor.isError && isNotFound(instructor.error) ? (
        <EmptyState
          title="This instructor isn’t taking bookings right now"
          description="They may be away or no longer listed. Browse other instructors near you."
          actionLabel="Browse instructors"
          onAction={() => navigate(routes.home)}
        />
      ) : null}
      {instructor.isError && !isNotFound(instructor.error) ? (
        <ErrorState
          title="Instructor unavailable"
          message={toUserMessage(instructor.error)}
          onRetry={() => void instructor.refetch()}
        />
      ) : null}
      {instructor.data ? (
        <article className="space-y-6">
          <header className="flex flex-wrap items-center gap-4 rounded-3xl bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.06)]">
            <InstructorAvatar name={instructor.data.displayName} photoUrl={instructor.data.photoUrls[0]} size="lg" />
            <div className="min-w-0 flex-1 space-y-1">
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="font-heading text-2xl font-medium">{instructor.data.displayName}</h1>
                {instructor.data.isVerified ? <VerifiedBadge /> : null}
              </div>
              {remoteOnly ? (
                <Chip
                  label={`Online · ${instructor.data.city}`}
                  size="small"
                  sx={{ fontWeight: 600, height: 24 }}
                />
              ) : (
                <p className="text-sm text-brand-muted">{instructor.data.area}</p>
              )}
              {instructor.data.reviewCount > 0 && instructor.data.ratingAverage != null ? (
                <p className="text-sm">
                  {instructor.data.ratingAverage.toFixed(1)} · {instructor.data.reviewCount}{" "}
                  {instructor.data.reviewCount === 1 ? "review" : "reviews"}
                </p>
              ) : null}
            </div>
            {instructor.data.areaOpen ? (
              <Button
                component="a"
                href={`#${BOOKING_ID}`}
                variant="contained"
                startIcon={<CalendarDays className="size-4" aria-hidden="true" />}
                sx={{ minHeight: 44, borderRadius: "14px", width: { xs: "100%", sm: "auto" }, display: { lg: "none" } }}
              >
                See open times
              </Button>
            ) : null}
          </header>

          <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_24rem] lg:gap-8">
            <div className="space-y-6">
              <InstructorGallery name={instructor.data.displayName} photoUrls={instructor.data.photoUrls} />
              <InstructorAbout instructor={instructor.data} />
              <IntroVideo name={instructor.data.displayName} videoId={instructor.data.introVideoId} />
              <InstructorCredentials certificates={instructor.data.certificates} />
              <ReviewList
                reviews={reviews.data}
                isLoading={reviews.isLoading}
                isError={reviews.isError}
                error={reviews.error}
                onRetry={() => void reviews.refetch()}
              />
            </div>

            <aside
              id={BOOKING_ID}
              aria-label="Book a session"
              className="scroll-mt-6 space-y-5 rounded-3xl border border-brand-border bg-brand-surface p-5 lg:sticky lg:top-20 lg:max-h-[calc(100svh-6rem)] lg:overflow-y-auto"
            >
              <h2 className="font-heading text-xl font-medium">Book a session</h2>
              <ModeRateList modes={modes} heading="Session fees" />
              {(remoteOnly || instructor.data.areaOpen) && instructor.data.trial ? (
                <TrialOfferNote name={instructor.data.displayName} trial={instructor.data.trial} />
              ) : null}
              {remoteOnly || instructor.data.areaOpen ? (
                <AvailabilitySection instructorId={instructor.data.id} modes={modes} />
              ) : (
                <EmptyState
                  title={areaClosedTitle}
                  description={areaClosedDescription}
                  actionLabel="Browse instructors"
                  onAction={() => navigate(routes.home)}
                />
              )}
            </aside>
          </div>
        </article>
      ) : null}
    </main>
  );
}
