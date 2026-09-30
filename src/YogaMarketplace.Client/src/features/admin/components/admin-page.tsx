import type { ReactNode } from "react";

type AdminPageProps = {
  kicker: string;
  title: string;
  lead: string;
  note?: string;
  children: ReactNode;
};

export function AdminPage({ kicker, title, lead, note, children }: AdminPageProps) {
  return (
    <main className="mx-auto max-w-6xl space-y-6 px-4 py-8 sm:px-8">
      <header className="space-y-1">
        <p className="text-[11px] font-medium tracking-[0.22em] text-brand-muted uppercase">{kicker}</p>
        <h1 className="font-heading text-2xl font-medium sm:text-3xl">{title}</h1>
        <p className="text-sm leading-relaxed text-brand-muted">{lead}</p>
        {note ? <p className="text-xs text-brand-muted">{note}</p> : null}
      </header>
      {children}
    </main>
  );
}
