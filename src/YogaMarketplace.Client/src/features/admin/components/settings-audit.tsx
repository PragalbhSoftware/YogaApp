import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useSettingsAudit } from "@/features/admin/hooks/use-admin";
import type { SettingsAuditEntry } from "@/features/admin/types";
import { formatAdminWhen } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";

const fieldLabels: Record<string, string> = {
  CommissionPercent: "Commission %",
  ConvenienceFee: "Convenience fee",
  CancelFreeWindowHours: "Free cancel window (h)",
  RescheduleFreeWindowHours: "Free reschedule window (h)",
  LateCancelFeeType: "Late-cancel fee type",
  LateCancelFeeValue: "Late-cancel fee",
  PayoutCycle: "Payout cycle",
  PolicyNote: "Internal note",
  BannerTitle: "Banner title",
  BannerSubtitle: "Banner subtitle",
  BannerOffer: "Banner offer",
};

export function SettingsAudit() {
  const audit = useSettingsAudit();

  return (
    <div className="space-y-3">
      <SettingsIntro title="Change history" lead="The last 50 changes, newest first." />
      <SettingsCard>
        {audit.isLoading ? <BookingListSkeleton /> : null}
        {audit.isError ? (
          <ErrorState message={toUserMessage(audit.error)} onRetry={() => void audit.refetch()} />
        ) : null}
        {audit.isSuccess && audit.data.length === 0 ? (
          <EmptyState title="No changes yet" description="Saved changes show up here with who made them." />
        ) : null}
        {audit.isSuccess && audit.data.length > 0 ? (
          <ul className="divide-y divide-brand-border">
            {audit.data.map((entry) => (
              <AuditRow key={entry.id} entry={entry} />
            ))}
          </ul>
        ) : null}
      </SettingsCard>
    </div>
  );
}

function AuditRow({ entry }: { entry: SettingsAuditEntry }) {
  return (
    <li className="flex flex-col gap-1 py-3 text-sm first:pt-0 last:pb-0">
      <p className="font-medium">{fieldLabels[entry.field] ?? entry.field}</p>
      <p className="break-words text-brand-muted">
        {entry.oldValue || "empty"} → {entry.newValue || "empty"}
      </p>
      <p className="text-xs text-brand-muted">
        {entry.adminName ?? "Admin"} · {formatAdminWhen(entry.changedAt)}
      </p>
    </li>
  );
}
