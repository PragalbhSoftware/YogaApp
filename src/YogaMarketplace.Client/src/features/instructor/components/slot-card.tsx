import { IconButton, Tooltip } from "@mui/material";
import { Ban, Pencil, Trash2, Unlock } from "lucide-react";
import { brand } from "@/constants/brand";
import type { OwnedSlot, SlotState } from "@/features/instructor/types";
import { durationLabel, formatTime12 } from "@/utils/clock";
import { cn } from "@/utils/cn";

type SlotCardProps = {
  slot: OwnedSlot;
  state: SlotState;
  busy: boolean;
  canManage: boolean;
  onBlock: (id: string) => void;
  onUnblock: (id: string) => void;
  onEdit: (slot: OwnedSlot) => void;
  onDelete: (slot: OwnedSlot) => void;
};

const labels: Record<SlotState, string> = {
  open: "Open",
  blocked: "Blocked",
  occupied: "Booked",
  past: "Past",
};

const statusClass: Record<SlotState, string> = {
  open: "text-brand-primary",
  blocked: "text-[#8A6A4A]",
  occupied: "text-brand-accent",
  past: "text-brand-muted",
};

export function SlotCard({
  slot,
  state,
  busy,
  canManage,
  onBlock,
  onUnblock,
  onEdit,
  onDelete,
}: SlotCardProps) {
  const canChange = canManage && (state === "open" || state === "blocked");
  const canDelete = canManage && state !== "occupied";
  const duration = durationLabel(slot.start, slot.end);

  return (
    <article className="flex items-center gap-3 rounded-[24px] border border-brand-border bg-brand-surface px-4 py-4 shadow-[0_8px_24px_rgba(37,49,39,0.04)] sm:gap-4 sm:px-5">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          <p className="font-heading text-xl font-medium tracking-tight sm:text-[22px]">
            {formatTime12(slot.start)}
          </p>
          <p className={cn("text-[11px] font-semibold tracking-[0.14em] uppercase", statusClass[state])}>
            {labels[state]}
          </p>
        </div>
        <p className="mt-0.5 text-sm text-brand-muted">
          to {formatTime12(slot.end)}
          {duration ? ` · ${duration}` : ""}
        </p>
      </div>

      {canChange || canDelete ? (
        <div className="flex shrink-0 items-center gap-0.5">
          {canChange ? (
            <Tooltip title="Edit">
              <span>
                <IconButton
                  aria-label="Edit slot"
                  disabled={busy}
                  onClick={() => onEdit(slot)}
                  sx={iconSx}
                >
                  <Pencil className="size-4" />
                </IconButton>
              </span>
            </Tooltip>
          ) : null}
          {canChange ? (
            state === "open" ? (
              <Tooltip title="Block">
                <span>
                  <IconButton
                    aria-label="Block slot"
                    disabled={busy}
                    onClick={() => onBlock(slot.id)}
                    sx={iconSx}
                  >
                    <Ban className="size-4" />
                  </IconButton>
                </span>
              </Tooltip>
            ) : (
              <Tooltip title="Unblock">
                <span>
                  <IconButton
                    aria-label="Unblock slot"
                    disabled={busy}
                    onClick={() => onUnblock(slot.id)}
                    sx={iconSx}
                  >
                    <Unlock className="size-4" />
                  </IconButton>
                </span>
              </Tooltip>
            )
          ) : null}
          {canDelete ? (
            <Tooltip title="Delete">
              <span>
                <IconButton
                  aria-label="Delete slot"
                  disabled={busy}
                  onClick={() => onDelete(slot)}
                  sx={{
                    ...iconSx,
                    "&:hover": { bgcolor: "rgba(214, 139, 101, 0.12)", color: brand.accent },
                  }}
                >
                  <Trash2 className="size-4" />
                </IconButton>
              </span>
            </Tooltip>
          ) : null}
        </div>
      ) : null}
    </article>
  );
}

const iconSx = {
  color: brand.textSecondary,
  width: 44,
  height: 44,
  borderRadius: "14px",
  "&:hover": { bgcolor: "rgba(101, 122, 91, 0.08)", color: brand.textPrimary },
};
