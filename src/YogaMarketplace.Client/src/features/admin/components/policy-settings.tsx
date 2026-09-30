import { Button, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { StatCard } from "@/features/admin/components/stat-card";
import { useAdminPolicy, useUpdatePolicy } from "@/features/admin/hooks/use-admin";
import { policySchema, type PolicyFormValues } from "@/features/admin/schemas";
import type { AdminPolicy } from "@/features/admin/types";
import { toUserMessage } from "@/services/http/api-error";

export function PolicySettings() {
  const policy = useAdminPolicy();

  if (policy.isLoading) return <BookingListSkeleton />;
  if (policy.isError) {
    return <ErrorState message={toUserMessage(policy.error)} onRetry={() => void policy.refetch()} />;
  }
  if (!policy.data) return null;

  return <PolicyForm policy={policy.data} />;
}

function PolicyForm({ policy }: { policy: AdminPolicy }) {
  const updatePolicy = useUpdatePolicy();
  const form = useForm<PolicyFormValues>({
    resolver: zodResolver(policySchema),
    values: {
      platformFeePercent: policy.platformFeePercent,
      cancelFreeWindowHours: policy.cancelFreeWindowHours,
      rescheduleFreeWindowHours: policy.rescheduleFreeWindowHours,
      lateCancelFeePercent: policy.lateCancelFeePercent,
      policyNote: policy.policyNote ?? "",
    },
  });

  return (
    <div className="space-y-5">
      <SettingsIntro
        title="Your take"
        lead="You keep this percent when a session is completed, marked no-show, or cancelled late. Payouts already created keep the fee captured at the time."
      />
      <StatCard
        label="Current take"
        value={`${policy.platformFeePercent}%`}
        hint={`${policy.currency} · applies to future payouts`}
      />
      <SettingsCard>
        <form
          className="space-y-5"
          onSubmit={form.handleSubmit((values) => {
            updatePolicy.mutate(values, {
              onSuccess: () => toast.success("Saved. Future payouts use this take."),
              onError: (error) => toast.error(toUserMessage(error)),
            });
          })}
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label="Platform fee percent"
              fullWidth
              inputMode="decimal"
              error={Boolean(form.formState.errors.platformFeePercent)}
              helperText={
                form.formState.errors.platformFeePercent?.message ??
                "Share of each completed session. 12–20% is typical while volume is early."
              }
              {...form.register("platformFeePercent")}
            />
            <TextField
              label="Late cancel fee percent"
              fullWidth
              inputMode="decimal"
              error={Boolean(form.formState.errors.lateCancelFeePercent)}
              helperText={
                form.formState.errors.lateCancelFeePercent?.message ??
                "Kept from accepted sessions cancelled inside the free window. The rest is refunded; the instructor gets the fee minus your take."
              }
              {...form.register("lateCancelFeePercent")}
            />
            <TextField
              label="Free cancel window (hours)"
              fullWidth
              inputMode="numeric"
              error={Boolean(form.formState.errors.cancelFreeWindowHours)}
              helperText={
                form.formState.errors.cancelFreeWindowHours?.message ?? "0 to 168 hours before the session."
              }
              {...form.register("cancelFreeWindowHours")}
            />
            <TextField
              label="Free reschedule window (hours)"
              fullWidth
              inputMode="numeric"
              error={Boolean(form.formState.errors.rescheduleFreeWindowHours)}
              helperText={
                form.formState.errors.rescheduleFreeWindowHours?.message ?? "0 to 168 hours before the session."
              }
              {...form.register("rescheduleFreeWindowHours")}
            />
          </div>
          <TextField
            label="Internal note"
            fullWidth
            multiline
            minRows={3}
            error={Boolean(form.formState.errors.policyNote)}
            helperText={form.formState.errors.policyNote?.message ?? "For you. Customers do not see this."}
            {...form.register("policyNote")}
            slotProps={{ htmlInput: { maxLength: 400 } }}
          />
          <Button
            type="submit"
            variant="contained"
            disabled={updatePolicy.isPending || !form.formState.isDirty}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {updatePolicy.isPending ? "Saving…" : "Save take"}
          </Button>
        </form>
      </SettingsCard>
    </div>
  );
}
