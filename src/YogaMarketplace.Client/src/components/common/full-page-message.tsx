import type { ReactNode } from "react";

type FullPageMessageProps = {
  kicker: string;
  title: string;
  message: string;
  actions: ReactNode;
  role?: "alert";
};

export function FullPageMessage({ kicker, title, message, actions, role }: FullPageMessageProps) {
  return (
    <main className="flex min-h-svh items-center justify-center bg-brand-background px-4 py-10">
      <div role={role} className="w-full max-w-md rounded-3xl bg-brand-surface px-6 py-10 text-center shadow-[0_8px_24px_rgba(37,49,39,0.05)]">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand-secondary">{kicker}</p>
        <h1 className="mt-3 font-heading text-2xl font-medium text-brand-text">{title}</h1>
        <p className="mt-2 text-sm leading-relaxed text-brand-muted">{message}</p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">{actions}</div>
      </div>
    </main>
  );
}
