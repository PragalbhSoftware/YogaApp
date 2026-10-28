import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { InstructorListSkeleton } from "@/components/common/loading-skeleton";
import type { SessionModeFilter } from "@/constants/catalog";
import { InstructorCard } from "@/features/marketplace/components/instructor-card";
import type { InstructorSummary } from "@/features/marketplace/types";
import { toUserMessage } from "@/services/http/api-error";

type InstructorListProps = {
  cityName: string;
  mode: SessionModeFilter;
  modeLabel: string;
  instructors: InstructorSummary[] | undefined;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  onRetry: () => void;
  onShowOnline: () => void;
};

export function InstructorList({
  cityName,
  mode,
  modeLabel,
  instructors,
  isLoading,
  isError,
  error,
  onRetry,
  onShowOnline,
}: InstructorListProps) {
  if (isLoading) return <InstructorListSkeleton />;

  if (isError) {
    return <ErrorState message={toUserMessage(error)} onRetry={onRetry} />;
  }

  const list = instructors ?? [];
  const inPerson = list.filter((item) => !item.isOutOfCity);
  const onlineOnly = list.filter((item) => item.isOutOfCity);

  if (list.length === 0) {
    const showOnlineAction = mode !== "Online";
    return (
      <EmptyState
        title={`No instructors in ${cityName} yet`}
        description={
          showOnlineAction
            ? `Nothing listed for ${modeLabel}. Try Online sessions from instructors in other cities.`
            : `Nothing listed for Online in ${cityName} yet.`
        }
        actionLabel={showOnlineAction ? "Show Online" : undefined}
        onAction={showOnlineAction ? onShowOnline : undefined}
      />
    );
  }

  return (
    <div className="space-y-4">
      {inPerson.length === 0 && onlineOnly.length > 0 ? (
        <p className="text-sm text-brand-muted">No in-person instructors in this city yet</p>
      ) : (
        <p className="text-sm text-brand-muted">
          {list.length === 1 ? "1 instructor" : `${list.length} instructors`}
        </p>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        {list.map((instructor) => (
          <InstructorCard key={instructor.id} instructor={instructor} mode={mode} />
        ))}
      </div>
    </div>
  );
}
