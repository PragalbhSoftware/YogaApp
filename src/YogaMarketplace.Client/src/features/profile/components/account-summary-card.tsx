import { Avatar, Button, Chip } from "@mui/material";
import { LogOut } from "lucide-react";
import { PhoneText } from "@/components/common/phone-text";
import { brand } from "@/constants/brand";

type AccountSummaryCardProps = {
  name?: string | null;
  phone?: string | null;
  role?: string | null;
  onSignOut: () => void;
};

function initialsOf(name?: string | null) {
  const parts = (name ?? "").trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  const first = parts[0][0] ?? "";
  const last = parts.length > 1 ? (parts[parts.length - 1][0] ?? "") : "";
  return (first + last).toUpperCase();
}

export function AccountSummaryCard({ name, phone, role, onSignOut }: AccountSummaryCardProps) {
  return (
    <section className="flex flex-col gap-5 rounded-3xl border border-brand-border bg-brand-surface p-5 sm:p-6">
      <div className="flex items-center gap-4">
        <Avatar
          sx={{
            width: 56,
            height: 56,
            bgcolor: brand.primary,
            color: brand.onPrimary,
            fontSize: 20,
            fontWeight: 500,
          }}
        >
          {initialsOf(name)}
        </Avatar>
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <h2 className="truncate font-heading text-lg font-medium text-brand-text">{name ?? "Signed in"}</h2>
          <p className="text-sm text-brand-muted">
            <PhoneText value={phone} />
          </p>
        </div>
        {role ? <Chip label={role} size="small" sx={{ flexShrink: 0 }} /> : null}
      </div>
      <Button
        variant="outlined"
        fullWidth
        startIcon={<LogOut className="size-4" aria-hidden="true" />}
        onClick={onSignOut}
        sx={{ minHeight: 44, borderRadius: "14px" }}
      >
        Sign out
      </Button>
    </section>
  );
}
