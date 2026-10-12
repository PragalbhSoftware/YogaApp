import { Link } from "react-router-dom";
import { Button } from "@mui/material";
import { teacherPath } from "@/constants/routes";
import type { SessionModeFilter } from "@/constants/catalog";
import { InstructorAvatar } from "@/features/marketplace/components/instructor-avatar";
import { ModeRateList } from "@/features/marketplace/components/mode-rate-list";
import { VerifiedBadge } from "@/features/marketplace/components/verified-badge";
import type { InstructorSummary } from "@/features/marketplace/types";

type InstructorCardProps = {
  instructor: InstructorSummary;
  mode?: SessionModeFilter;
};

export function InstructorCard({ instructor, mode = "" }: InstructorCardProps) {
  const modes = instructor.modes ?? [];
  const href = mode ? `${teacherPath(instructor.id)}?mode=${mode}` : teacherPath(instructor.id);

  return (
    <article className="flex h-full flex-col rounded-3xl bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.06)]">
      <div className="flex gap-4">
        <InstructorAvatar name={instructor.displayName} photoUrl={instructor.photoUrl} size="md" />
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="font-heading text-xl leading-tight font-medium">
              {instructor.displayName}
            </h2>
            {instructor.isVerified ? <VerifiedBadge /> : null}
          </div>
          <p className="text-sm text-brand-muted">{instructor.area}</p>
          {instructor.reviewCount > 0 && instructor.ratingAverage != null ? (
            <p className="text-sm text-brand-text">
              {instructor.ratingAverage.toFixed(1)} · {instructor.reviewCount}{" "}
              {instructor.reviewCount === 1 ? "review" : "reviews"}
            </p>
          ) : null}
        </div>
      </div>

      {modes.length > 0 ? (
        <div className="mt-4">
          <ModeRateList modes={modes} />
        </div>
      ) : null}

      <Button
        className="mt-auto pt-5"
        component={Link}
        to={href}
        variant="contained"
        fullWidth
        sx={{ mt: 2.5 }}
        aria-label={`View ${instructor.displayName} and book`}
      >
        View & book
      </Button>
    </article>
  );
}
