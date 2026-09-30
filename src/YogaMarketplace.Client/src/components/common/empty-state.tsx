import { Button, Typography } from "@mui/material";

type EmptyStateProps = {
  title: string;
  description: string;
  actionLabel?: string;
  onAction?: () => void;
};

export function EmptyState({ title, description, actionLabel, onAction }: EmptyStateProps) {
  return (
    <div className="rounded-3xl bg-brand-surface px-5 py-10 text-center shadow-[0_8px_24px_rgba(37,49,39,0.05)]">
      <Typography variant="h6" className="font-heading">
        {title}
      </Typography>
      <p className="mt-2 text-sm leading-relaxed text-brand-muted">{description}</p>
      {actionLabel && onAction ? (
        <Button className="mt-5" onClick={onAction} variant="outlined">
          {actionLabel}
        </Button>
      ) : null}
    </div>
  );
}
