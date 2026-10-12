import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { CertificateManager } from "@/features/instructor/components/certificate-manager";
import { IntroVideoForm } from "@/features/instructor/components/intro-video-form";
import { PhotoManager } from "@/features/instructor/components/photo-manager";
import { useInstructorMedia } from "@/features/instructor/hooks/use-instructor-schedule";
import { VerifiedBadge } from "@/features/marketplace/components/verified-badge";
import { toUserMessage } from "@/services/http/api-error";

type ProfileMediaSectionProps = {
  displayName: string;
};

export function ProfileMediaSection({ displayName }: ProfileMediaSectionProps) {
  const media = useInstructorMedia();

  return (
    <section className="space-y-5">
      <div className="space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="font-heading text-lg font-medium">Photos, video and certificates</h2>
          {media.data?.isVerified ? <VerifiedBadge /> : null}
        </div>
        <p className="text-sm leading-relaxed text-brand-muted">
          These help customers trust you before they book.
        </p>
      </div>
      {media.isLoading ? <BookingListSkeleton /> : null}
      {media.isError ? (
        <ErrorState message={toUserMessage(media.error)} onRetry={() => void media.refetch()} />
      ) : null}
      {media.data ? (
        <>
          <PhotoManager photos={media.data.photos} />
          <IntroVideoForm name={displayName} videoId={media.data.introVideoId} />
          <CertificateManager certificates={media.data.certificates} />
        </>
      ) : null}
    </section>
  );
}
