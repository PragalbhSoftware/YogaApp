import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { CertificateManager } from "@/features/instructor/components/certificate-manager";
import { IntroVideoForm } from "@/features/instructor/components/intro-video-form";
import { MediaHeading } from "@/features/instructor/components/media-heading";
import { PhotoManager } from "@/features/instructor/components/photo-manager";
import { useInstructorMedia } from "@/features/instructor/hooks/use-instructor-schedule";
import { VerifiedBadge } from "@/features/marketplace/components/verified-badge";
import { toUserMessage } from "@/services/http/api-error";

const cardClass = "flex flex-col gap-6 rounded-3xl border border-brand-border bg-brand-surface p-5 sm:p-6";

type ProfileMediaSectionProps = {
  displayName: string;
};

/** Photos and video in one card, certificates in their own card; side by side on large screens. */
export function ProfileMediaSection({ displayName }: ProfileMediaSectionProps) {
  const media = useInstructorMedia();

  if (media.isLoading) return <BookingListSkeleton />;
  if (media.isError) return <ErrorState message={toUserMessage(media.error)} onRetry={() => void media.refetch()} />;
  if (!media.data) return null;

  return (
    <div className="grid items-start gap-6 lg:grid-cols-2">
      <section className={cardClass}>
        <MediaHeading
          level="card"
          title="Photos and video"
          caption="These help customers trust you before they book."
        />
        <PhotoManager photos={media.data.photos} />
        <IntroVideoForm name={displayName} videoId={media.data.introVideoId} />
      </section>
      <CertificateManager
        certificates={media.data.certificates}
        badge={media.data.isVerified ? <VerifiedBadge /> : null}
      />
    </div>
  );
}
