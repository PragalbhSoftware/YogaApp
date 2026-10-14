import { Button, FormControlLabel, Switch, TextField } from "@mui/material";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useAdminSettings, useUpdateSettings } from "@/features/admin/hooks/use-admin";
import { notificationSettingsSchema, type NotificationSettingsValues } from "@/features/admin/schemas";
import type { AdminSettings } from "@/features/admin/types";
import { changedFields, settingsSaveError } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";

function toFormValues(settings: AdminSettings): NotificationSettingsValues {
  return {
    emailNotificationsEnabled: settings.emailNotificationsEnabled,
    notificationMaxAttempts: settings.notificationMaxAttempts,
  };
}

export function NotificationSettings() {
  const settings = useAdminSettings();

  if (settings.isLoading) return <BookingListSkeleton />;
  if (settings.isError) {
    return <ErrorState message={toUserMessage(settings.error)} onRetry={() => void settings.refetch()} />;
  }
  if (!settings.data) return null;
  return <NotificationForm settings={settings.data} />;
}

function NotificationForm({ settings }: { settings: AdminSettings }) {
  const updateSettings = useUpdateSettings();
  const form = useForm<NotificationSettingsValues>({
    resolver: zodResolver(notificationSettingsSchema),
    values: toFormValues(settings),
  });
  const errors = form.formState.errors;

  return (
    <div className="space-y-5">
      <SettingsIntro
        title="Booking emails"
        lead="Emails to customers and instructors when a booking is confirmed, cancelled or refunded. Customers without an email on file are skipped."
      />
      <SettingsCard>
        <form
          noValidate
          className="space-y-6"
          onSubmit={form.handleSubmit((values) =>
            updateSettings.mutate(
              { version: settings.version, ...changedFields(values, form.formState.dirtyFields) },
              {
                onSuccess: () => toast.success("Booking emails saved."),
                onError: (error) => toast.error(settingsSaveError(error)),
              },
            ),
          )}
        >
          <Controller
            name="emailNotificationsEnabled"
            control={form.control}
            render={({ field }) => (
              <div>
                <FormControlLabel
                  sx={{ ml: 0 }}
                  control={<Switch checked={field.value} onChange={(_, checked) => field.onChange(checked)} />}
                  label="Emails to customers and instructors"
                />
                <p className="text-sm text-brand-muted">
                  Off by default. While off, or without SMTP set up on the server, emails are skipped, not failed.
                </p>
              </div>
            )}
          />

          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label="Tries per email"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.notificationMaxAttempts)}
              helperText={errors.notificationMaxAttempts?.message ?? "A failed send is retried with growing waits, up to this many tries."}
              {...form.register("notificationMaxAttempts")}
            />
          </div>

          <Button
            type="submit"
            variant="contained"
            disabled={updateSettings.isPending || !form.formState.isDirty}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {updateSettings.isPending ? "Saving…" : "Save booking emails"}
          </Button>
        </form>
      </SettingsCard>
    </div>
  );
}
