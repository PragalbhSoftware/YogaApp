import { Link } from "react-router-dom";

type QueueCardProps = {
  to: string;
  label: string;
  value: number;
  hint: string;
};

export function QueueCard({ to, label, value, hint }: QueueCardProps) {
  return (
    <Link
      to={to}
      className="block rounded-[24px] border border-brand-border bg-brand-surface px-5 py-5 shadow-[0_8px_24px_rgba(37,49,39,0.04)]"
    >
      <p className="text-[11px] font-medium tracking-[0.16em] text-brand-muted uppercase">{label}</p>
      <p className="mt-2 font-heading text-2xl font-medium tracking-tight">{value}</p>
      <p className="mt-1 text-sm text-brand-muted">{hint}</p>
    </Link>
  );
}
