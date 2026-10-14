import type { ReactNode } from "react";
import { Button, FormControlLabel, Switch, TextField } from "@mui/material";
import { Controller, useForm, type Control } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useAdminSettings, useUpdateSettings } from "@/features/admin/hooks/use-admin";
import { jobScheduleSettingsSchema, type JobScheduleSettingsValues } from "@/features/admin/schemas";
import type { AdminSettings } from "@/features/admin/types";
import { changedFields, settingsSaveError } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";
import { toastError } from "@/services/http/toast-error";

type SwitchName = "reconciliationEnabled" | "creditExpiryEnabled" | "webhookCleanupEnabled";

function toFormValues(settings: AdminSettings): JobScheduleSettingsValues {
  return {
    reconciliationEnabled: settings.reconciliationEnabled,
    creditExpiryEnabled: settings.creditExpiryEnabled,
    creditExpiryTime: settings.creditExpiryTime,
    webhookCleanupEnabled: settings.webhookCleanupEnabled,
    webhookCleanupTime: settings.webhookCleanupTime,
  };
}

export function JobScheduleSettings() {
  const settings = useAdminSettings();

  if (settings.isLoading) return <BookingListSkeleton />;
  if (settings.isError) {
    return <ErrorState message={toUserMessage(settings.error)} onRetry={() => void settings.refetch()} />;
  }
  if (!settings.data) return null;
  return <JobScheduleForm settings={settings.data} />;
}

function JobScheduleForm({ settings }: { settings: AdminSettings }) {
  const updateSettings = useUpdateSettings();
  const form = useForm<JobScheduleSettingsValues>({
    resolver: zodResolver(jobScheduleSettingsSchema),
    values: toFormValues(settings),
  });
  const errors = form.formState.errors;

  return (
    <div className="space-y-5">
      <SettingsIntro
        title="Schedules"
        lead="Daily jobs run at these India times. A change moves the next run within a minute or two. Turning a job off stops it until you turn it back on."
      />
      <SettingsCard>
        <form
          noValidate
          className="space-y-6"
          onSubmit={form.handleSubmit((values) =>
            updateSettings.mutate(
              { version: settings.version, ...changedFields(values, form.formState.dirtyFields) },
              {
                onSuccess: () => toast.success("Schedules saved."),
                onError: (error) => toastError(error, settingsSaveError(error)),
              },
            ),
          )}
        >
          <ScheduleRow
            control={form.control}
            name="reconciliationEnabled"
            label="Payment reconciliation"
            hint={`Daily at ${settings.reconciliationTime}. Change the time under Settings › Fees.`}
          />
          <ScheduleRow
            control={form.control}
            name="creditExpiryEnabled"
            label="Expire unused credits"
            hint="Takes back credits past their pack’s last day."
          >
            <TextField
              label="Runs at"
              type="time"
              slotProps={{ inputLabel: { shrink: true } }}
              error={Boolean(errors.creditExpiryTime)}
              helperText={errors.creditExpiryTime?.message ?? "India time"}
              {...form.register("creditExpiryTime")}
            />
          </ScheduleRow>
          <ScheduleRow
            control={form.control}
            name="webhookCleanupEnabled"
            label="Clear old webhook payloads"
            hint="Blanks raw Razorpay payloads older than the retention set under Settings › Pages."
          >
            <TextField
              label="Runs at"
              type="time"
              slotProps={{ inputLabel: { shrink: true } }}
              error={Boolean(errors.webhookCleanupTime)}
              helperText={errors.webhookCleanupTime?.message ?? "India time"}
              {...form.register("webhookCleanupTime")}
            />
          </ScheduleRow>

          <Button
            type="submit"
            variant="contained"
            disabled={updateSettings.isPending || !form.formState.isDirty}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {updateSettings.isPending ? "Saving…" : "Save schedules"}
          </Button>
        </form>
      </SettingsCard>
    </div>
  );
}

type ScheduleRowProps = {
  control: Control<JobScheduleSettingsValues>;
  name: SwitchName;
  label: string;
  hint: string;
  children?: ReactNode;
};

function ScheduleRow({ control, name, label, hint, children }: ScheduleRowProps) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-4">
      <Controller
        name={name}
        control={control}
        render={({ field }) => (
          <div className="min-w-0 flex-1">
            <FormControlLabel
              sx={{ ml: 0 }}
              control={<Switch checked={field.value} onChange={(_, checked) => field.onChange(checked)} />}
              label={label}
            />
            <p className="text-sm text-brand-muted">{hint}</p>
          </div>
        )}
      />
      {children}
    </div>
  );
}
