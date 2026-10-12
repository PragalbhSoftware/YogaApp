import type { ReactNode } from "react";

type FactCardProps = {
  title: string;
  children: ReactNode;
};

export function FactCard({ title, children }: FactCardProps) {
  return (
    <section className="flex flex-col gap-4 rounded-[24px] border border-brand-border bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.04)]">
      <h2 className="font-heading text-lg font-medium">{title}</h2>
      <dl className="grid gap-4 text-sm sm:grid-cols-2">{children}</dl>
    </section>
  );
}

type FactProps = {
  label: string;
  children: ReactNode;
  wide?: boolean;
};

export function Fact({ label, children, wide }: FactProps) {
  return (
    <div className={wide ? "sm:col-span-2" : undefined}>
      <dt className="text-brand-muted">{label}</dt>
      <dd className="mt-0.5 break-words">{children}</dd>
    </div>
  );
}
