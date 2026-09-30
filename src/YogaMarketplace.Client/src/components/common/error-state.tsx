import { Button, Typography } from "@mui/material";

type ErrorStateProps = {
  title?: string;
  message: string;
  onRetry?: () => void;
};

export function ErrorState({ title = "Couldn’t load this", message, onRetry }: ErrorStateProps) {
  return (
    <div
      className="rounded-3xl border border-red-200 bg-brand-surface px-5 py-8 text-center"
      role="alert"
    >
      <Typography variant="h6" className="font-heading">
        {title}
      </Typography>
      <p className="mt-2 text-sm leading-relaxed text-brand-muted">{message}</p>
      {onRetry ? (
        <Button className="mt-5" onClick={onRetry} variant="contained">
          Try again
        </Button>
      ) : null}
    </div>
  );
}
