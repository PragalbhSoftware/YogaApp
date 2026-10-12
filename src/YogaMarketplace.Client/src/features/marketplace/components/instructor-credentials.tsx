import { Award } from "lucide-react";

type InstructorCredentialsProps = {
  certificates: string[];
};

export function InstructorCredentials({ certificates }: InstructorCredentialsProps) {
  if (certificates.length === 0) return null;
  return (
    <section className="space-y-2">
      <h2 className="font-heading text-lg font-medium">Checked certificates</h2>
      <ul className="space-y-2">
        {certificates.map((title) => (
          <li key={title} className="flex items-start gap-2 text-sm text-brand-text">
            <Award className="mt-0.5 size-4 shrink-0 text-brand-primary" aria-hidden="true" />
            {title}
          </li>
        ))}
      </ul>
    </section>
  );
}
