import { CircularProgress } from "@mui/material";

export function PageLoader() {
  return (
    <div className="flex min-h-[40vh] items-center justify-center" role="status" aria-label="Loading">
      <CircularProgress />
    </div>
  );
}
