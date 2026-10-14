import { Button, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useAdminSettings, useUpdateSettings } from "@/features/admin/hooks/use-admin";
import { signInLimitSettingsSchema, type SignInLimitSettingsValues } from "@/features/admin/schemas";
import type { AdminSettings } from "@/features/admin/types";
import { changedFields } from "@/features/admin/utils";
import { keepUnsavedEdits, settingsSaveHandlers } from "@/features/admin/settings-form";
import { toUserMessage } from "@/services/http/api-error";

function toFormValues(settings: AdminSettings): SignInLimitSettingsValues {
  return {
    otpMaxSendsPerPhone: settings.otpMaxSendsPerPhone,
    otpMaxWrongCodesPerPhone: settings.otpMaxWrongCodesPerPhone,
    otpLockMinutes: settings.otpLockMinutes,
  };
}

export function SignInLimitSettings() {
  const settings = useAdminSettings();

  if (settings.isLoading) return <BookingListSkeleton />;
  if (settings.isError) {
    return <ErrorState message={toUserMessage(settings.error)} onRetry={() => void settings.refetch()} />;
  }
  if (!settings.data) return null;
  return <SignInLimitForm settings={settings.data} />;
}

function SignInLimitForm({ settings }: { settings: AdminSettings }) {
  const updateSettings = useUpdateSettings();
  const form = useForm<SignInLimitSettingsValues>({
    resolver: zodResolver(signInLimitSettingsSchema),
    values: toFormValues(settings),
    resetOptions: keepUnsavedEdits,
  });
  const errors = form.formState.errors;

  return (
    <div className="space-y-5">
      <SettingsIntro
        title="Sign-in code limits"
        lead="Stops one phone number from being flooded with codes or guessed at. Codes and wrong tries are counted over the lock time; reaching either limit locks that number for the lock time."
      />
      <SettingsCard>
        <form
          noValidate
          className="space-y-6"
          onSubmit={form.handleSubmit((values) =>
            updateSettings.mutate(
              { version: settings.version, ...changedFields(values, form.formState.dirtyFields) },
              settingsSaveHandlers(form, toFormValues, "Sign-in limits saved."),
            ),
          )}
        >
          <div className="grid gap-4 sm:grid-cols-3">
            <TextField
              label="Codes per phone"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.otpMaxSendsPerPhone)}
              helperText={errors.otpMaxSendsPerPhone?.message ?? "Codes one number can ask for within the lock time."}
              {...form.register("otpMaxSendsPerPhone")}
            />
            <TextField
              label="Wrong codes per phone"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.otpMaxWrongCodesPerPhone)}
              helperText={errors.otpMaxWrongCodesPerPhone?.message ?? "Wrong tries across all of that number's codes."}
              {...form.register("otpMaxWrongCodesPerPhone")}
            />
            <TextField
              label="Lock minutes"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.otpLockMinutes)}
              helperText={errors.otpLockMinutes?.message ?? "How long a number stays locked."}
              {...form.register("otpLockMinutes")}
            />
          </div>

          <Button
            type="submit"
            variant="contained"
            disabled={updateSettings.isPending || !form.formState.isDirty}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {updateSettings.isPending ? "Saving…" : "Save sign-in limits"}
          </Button>
        </form>
      </SettingsCard>
    </div>
  );
}
