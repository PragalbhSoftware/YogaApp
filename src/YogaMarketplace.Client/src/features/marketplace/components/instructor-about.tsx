import { instructorIntro, practiceSummary, sessionOfferLine } from "@/features/marketplace/utils/copy";
import type { InstructorDetail } from "@/features/marketplace/types";

type InstructorAboutProps = {
  instructor: InstructorDetail;
};

export function InstructorAbout({ instructor }: InstructorAboutProps) {
  const verified = instructor.status.toLowerCase() === "verified";
  const practice = practiceSummary(instructor.bio);

  return (
    <section className="space-y-2">
      <h2 className="font-heading text-lg font-medium">About</h2>
      <p className="text-sm leading-relaxed text-brand-text">
        {instructorIntro(instructor.displayName, instructor.area, verified)}
      </p>
      {practice ? <p className="text-sm leading-relaxed text-brand-text">{practice}</p> : null}
      <p className="text-sm leading-relaxed text-brand-muted">
        {sessionOfferLine(instructor.modes, instructor.area)}
      </p>
    </section>
  );
}
