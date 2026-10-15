import { Button, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard } from "@/features/admin/components/settings-card";
import { useAdminSettings, useUpdateSettings } from "@/features/admin/hooks/use-admin";
import { privacySettingsSchema, type PrivacySettingsValues } from "@/features/admin/schemas";
import type { AdminSettings } from "@/features/admin/types";
import { changedFields, settingsSaveError } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";
import { toastError } from "@/services/http/toast-error";

export function PrivacySettings() {
  const settings = useAdminSettings();

  if (settings.isLoading) return <BookingListSkeleton />;
  if (settings.isError) {
    return <ErrorState message={toUserMessage(settings.error)} onRetry={() => void settings.refetch()} />;
  }
  if (!settings.data) return null;
  return <PrivacyForm settings={settings.data} />;
}

function PrivacyForm({ settings }: { settings: AdminSettings }) {
  const updateSettings = useUpdateSettings();
  const form = useForm<PrivacySettingsValues>({
    resolver: zodResolver(privacySettingsSchema(settings.policyVersion)),
    values: {
      policyVersion: settings.policyVersion,
      webhookPayloadRetentionDays: settings.webhookPayloadRetentionDays,
    },
  });
  const errors = form.formState.errors;

  return (
    <div className="space-y-5">
      <SettingsCard>
        <form
          noValidate
          className="space-y-5"
          onSubmit={form.handleSubmit((values) =>
            updateSettings.mutate(
              { version: settings.version, ...changedFields(values, form.formState.dirtyFields) },
              {
                onSuccess: () => toast.success("Saved."),
                onError: (error) => toastError(error, settingsSaveError(error)),
              },
            ),
          )}
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label="Policy version"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.policyVersion)}
              helperText={errors.policyVersion?.message ?? "Users who accepted an older version are asked once."}
              {...form.register("policyVersion")}
            />
            <TextField
              label="Keep webhook payloads (days)"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.webhookPayloadRetentionDays)}
              helperText={
                errors.webhookPayloadRetentionDays?.message ??
                "Older raw Razorpay payloads are cleared nightly. The event log stays."
              }
              {...form.register("webhookPayloadRetentionDays")}
            />
          </div>

          <Button
            type="submit"
            variant="contained"
            disabled={updateSettings.isPending || !form.formState.isDirty}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {updateSettings.isPending ? "Saving…" : "Save consent and data"}
          </Button>
        </form>
      </SettingsCard>
    </div>
  );
}
