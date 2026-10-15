import { Button, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useAdminSettings, useUpdateSettings } from "@/features/admin/hooks/use-admin";
import { reconciliationSettingsSchema, type ReconciliationSettingsValues } from "@/features/admin/schemas";
import type { AdminSettings } from "@/features/admin/types";
import { changedFields } from "@/features/admin/utils";
import { keepUnsavedEdits, settingsSaveHandlers } from "@/features/admin/settings-form";
import { toUserMessage } from "@/services/http/api-error";

function toFormValues(settings: AdminSettings): ReconciliationSettingsValues {
  return {
    reconciliationTime: settings.reconciliationTime,
    webhookStuckMinutes: settings.webhookStuckMinutes,
    refundPendingHours: settings.refundPendingHours,
  };
}

export function ReconciliationSettings() {
  const settings = useAdminSettings();

  if (settings.isLoading) return <BookingListSkeleton />;
  if (settings.isError) {
    return <ErrorState message={toUserMessage(settings.error)} onRetry={() => void settings.refetch()} />;
  }
  if (!settings.data) return null;
  return <ReconciliationForm settings={settings.data} />;
}

function ReconciliationForm({ settings }: { settings: AdminSettings }) {
  const updateSettings = useUpdateSettings();
  const form = useForm<ReconciliationSettingsValues>({
    resolver: zodResolver(reconciliationSettingsSchema),
    values: toFormValues(settings),
    resetOptions: keepUnsavedEdits,
  });
  const errors = form.formState.errors;

  return (
    <div className="space-y-5">
      <SettingsIntro
        title="Payment reconciliation"
        lead="Once a day we compare Razorpay with our records for the previous day and list anything that differs under Transactions. Nothing is changed automatically."
      />
      <SettingsCard>
        <form
          noValidate
          className="space-y-5"
          onSubmit={form.handleSubmit((values) =>
            updateSettings.mutate(
              { version: settings.version, ...changedFields(values, form.formState.dirtyFields) },
              settingsSaveHandlers(form, toFormValues, "Saved. The next daily check uses these values."),
            ),
          )}
        >
          <div className="grid gap-4 sm:grid-cols-3">
            <TextField
              label="Daily check time"
              type="time"
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
              error={Boolean(errors.reconciliationTime)}
              helperText={errors.reconciliationTime?.message ?? "India time. A change moves the next check within a minute or two."}
              {...form.register("reconciliationTime")}
            />
            <TextField
              label="Webhook stuck after (minutes)"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.webhookStuckMinutes)}
              helperText={errors.webhookStuckMinutes?.message ?? "A webhook not processed by then is listed."}
              {...form.register("webhookStuckMinutes")}
            />
            <TextField
              label="Refund pending after (hours)"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.refundPendingHours)}
              helperText={errors.refundPendingHours?.message ?? "A refund Razorpay hasn’t confirmed by then is listed."}
              {...form.register("refundPendingHours")}
            />
          </div>

          <Button
            type="submit"
            variant="contained"
            disabled={updateSettings.isPending || !form.formState.isDirty}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {updateSettings.isPending ? "Saving…" : "Save reconciliation"}
          </Button>
        </form>
      </SettingsCard>
    </div>
  );
}
