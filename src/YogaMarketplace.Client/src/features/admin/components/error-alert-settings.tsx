import { Button, FormControlLabel, Switch, TextField } from "@mui/material";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useAdminSettings, useSendTestAlert, useUpdateSettings } from "@/features/admin/hooks/use-admin";
import { errorAlertSettingsSchema, type ErrorAlertSettingsValues } from "@/features/admin/schemas";
import type { AdminSettings } from "@/features/admin/types";
import { changedFields } from "@/features/admin/utils";
import { keepUnsavedEdits, settingsSaveHandlers } from "@/features/admin/settings-form";
import { toUserMessage } from "@/services/http/api-error";
import { toastError } from "@/services/http/toast-error";

function toFormValues(settings: AdminSettings): ErrorAlertSettingsValues {
  return {
    emailAlertsEnabled: settings.emailAlertsEnabled,
    errorAlertEmail: settings.errorAlertEmail ?? "",
    errorAlertThrottleMinutes: settings.errorAlertThrottleMinutes,
    errorAlertCheckMinutes: settings.errorAlertCheckMinutes,
    errorEventRetentionDays: settings.errorEventRetentionDays,
  };
}

export function ErrorAlertSettings() {
  const settings = useAdminSettings();

  if (settings.isLoading) return <BookingListSkeleton />;
  if (settings.isError) {
    return <ErrorState message={toUserMessage(settings.error)} onRetry={() => void settings.refetch()} />;
  }
  if (!settings.data) return null;
  return <ErrorAlertForm settings={settings.data} />;
}

function ErrorAlertForm({ settings }: { settings: AdminSettings }) {
  const updateSettings = useUpdateSettings();
  const sendTest = useSendTestAlert();
  const form = useForm<ErrorAlertSettingsValues>({
    resolver: zodResolver(errorAlertSettingsSchema),
    values: toFormValues(settings),
    resetOptions: keepUnsavedEdits,
  });
  const errors = form.formState.errors;
  const canSendTest = settings.emailAlertsEnabled && !form.formState.isDirty && !sendTest.isPending;

  return (
    <div className="space-y-5">
      <SettingsIntro
        title="Error alerts"
        lead="Every server error shows under Errors. Turn on email alerts to also get an email, at most once per kind of error in each quiet period."
      />
      <SettingsCard>
        <form
          noValidate
          className="space-y-6"
          onSubmit={form.handleSubmit((values) =>
            updateSettings.mutate(
              { version: settings.version, ...changedFields(values, form.formState.dirtyFields) },
              settingsSaveHandlers(form, toFormValues, "Error alerts saved."),
            ),
          )}
        >
          <Controller
            name="emailAlertsEnabled"
            control={form.control}
            render={({ field }) => (
              <div>
                <FormControlLabel
                  sx={{ ml: 0 }}
                  control={<Switch checked={field.value} onChange={(_, checked) => field.onChange(checked)} />}
                  label="Email alerts"
                />
                <p className="text-sm text-brand-muted">Off by default. Errors are still recorded when this is off.</p>
              </div>
            )}
          />

          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label="Send alerts to"
              type="email"
              autoComplete="email"
              fullWidth
              error={Boolean(errors.errorAlertEmail)}
              helperText={errors.errorAlertEmail?.message ?? "One address, for example ops@yourdomain.in"}
              {...form.register("errorAlertEmail")}
            />
            <TextField
              label="Quiet period per error (minutes)"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.errorAlertThrottleMinutes)}
              helperText={errors.errorAlertThrottleMinutes?.message ?? "At most one email per kind of error in this time."}
              {...form.register("errorAlertThrottleMinutes")}
            />
            <TextField
              label="Check for new errors every (minutes)"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.errorAlertCheckMinutes)}
              helperText={errors.errorAlertCheckMinutes?.message ?? "How soon an alert goes out after an error."}
              {...form.register("errorAlertCheckMinutes")}
            />
            <TextField
              label="Keep errors (days)"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.errorEventRetentionDays)}
              helperText={errors.errorEventRetentionDays?.message ?? "Errors not seen for this long are deleted."}
              {...form.register("errorEventRetentionDays")}
            />
          </div>

          <div className="flex flex-wrap gap-3">
            <Button
              type="submit"
              variant="contained"
              disabled={updateSettings.isPending || !form.formState.isDirty}
              sx={{ minHeight: 44, borderRadius: "14px" }}
            >
              {updateSettings.isPending ? "Saving…" : "Save error alerts"}
            </Button>
            <Button
              variant="outlined"
              disabled={!canSendTest}
              onClick={() =>
                sendTest.mutate(undefined, {
                  onSuccess: () => toast.success(`Test email sent to ${settings.errorAlertEmail ?? "the alert address"}.`),
                  onError: (error) => toastError(error),
                })
              }
              sx={{ minHeight: 44, borderRadius: "14px" }}
            >
              {sendTest.isPending ? "Sending…" : "Send test email"}
            </Button>
          </div>
          {!settings.emailAlertsEnabled ? (
            <p className="text-sm text-brand-muted">Turn email alerts on and save to send a test email.</p>
          ) : null}
        </form>
      </SettingsCard>
    </div>
  );
}
