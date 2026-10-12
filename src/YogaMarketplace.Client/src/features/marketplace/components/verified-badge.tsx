import { Tooltip } from "@mui/material";
import { ShieldCheck } from "lucide-react";

export function VerifiedBadge() {
  return (
    <Tooltip title="Admin has checked at least one of this instructor's certificates.">
      <span className="inline-flex items-center gap-1 rounded-full bg-brand-primary/10 px-2 py-0.5 text-xs font-medium text-brand-primary">
        <ShieldCheck className="size-3.5" aria-hidden="true" />
        Verified
      </span>
    </Tooltip>
  );
}
