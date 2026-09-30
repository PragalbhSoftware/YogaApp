import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { InstructorListSkeleton } from "@/components/common/loading-skeleton";
import type { SessionModeFilter } from "@/constants/catalog";
import { InstructorCard } from "@/features/marketplace/components/instructor-card";
import type { InstructorSummary } from "@/features/marketplace/types";
import { toUserMessage } from "@/services/http/api-error";

type InstructorListProps = {
  areaName: string;
  mode: SessionModeFilter;
  modeLabel: string;
  instructors: InstructorSummary[] | undefined;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  onRetry: () => void;
  onChangeArea: () => void;
};

export function InstructorList({
  areaName,
  mode,
  modeLabel,
  instructors,
  isLoading,
  isError,
  error,
  onRetry,
  onChangeArea,
}: InstructorListProps) {
  if (isLoading) return <InstructorListSkeleton />;

  if (isError) {
    return <ErrorState message={toUserMessage(error)} onRetry={onRetry} />;
  }

  const list = instructors ?? [];
  if (list.length === 0) {
    return (
      <EmptyState
        title={`No instructors in ${areaName} yet`}
        description={`Nothing listed for ${modeLabel}. Try another neighbourhood or session type.`}
        actionLabel="Change area"
        onAction={onChangeArea}
      />
    );
  }

  return (
    <div className="space-y-4">
      <p className="text-sm text-brand-muted">
        {list.length === 1 ? "1 instructor" : `${list.length} instructors`}
      </p>
      <div className="grid gap-4 sm:grid-cols-2">
        {list.map((instructor) => (
          <InstructorCard key={instructor.id} instructor={instructor} mode={mode} />
        ))}
      </div>
    </div>
  );
}
