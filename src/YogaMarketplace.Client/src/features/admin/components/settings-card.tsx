import type { ReactNode } from "react";
import { cn } from "@/utils/cn";

type SettingsCardProps = {
  children: ReactNode;
  className?: string;
};

export function SettingsCard({ children, className }: SettingsCardProps) {
  return (
    <div className={cn("rounded-[24px] border border-brand-border bg-brand-surface p-5 sm:p-6", className)}>
      {children}
    </div>
  );
}

type SettingsIntroProps = {
  title: string;
  lead: string;
};

export function SettingsIntro({ title, lead }: SettingsIntroProps) {
  return (
    <div className="space-y-1">
      <h2 className="font-heading text-lg font-medium">{title}</h2>
      <p className="text-sm leading-relaxed text-brand-muted">{lead}</p>
    </div>
  );
}
