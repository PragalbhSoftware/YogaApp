import { Button, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard } from "@/features/admin/components/settings-card";
import { useAdminSettings, useUpdateSettings } from "@/features/admin/hooks/use-admin";
import { nearestCitySettingsSchema, type NearestCitySettingsValues } from "@/features/admin/schemas";
import type { AdminSettings } from "@/features/admin/types";
import { changedFields } from "@/features/admin/utils";
import { keepUnsavedEdits, settingsSaveHandlers } from "@/features/admin/settings-form";
import { toUserMessage } from "@/services/http/api-error";

function toFormValues(settings: AdminSettings): NearestCitySettingsValues {
  return { nearestCityMaxKm: settings.nearestCityMaxKm };
}

export function NearestCitySettings() {
  const settings = useAdminSettings();

  if (settings.isLoading) return <BookingListSkeleton />;
  if (settings.isError) {
    return <ErrorState message={toUserMessage(settings.error)} onRetry={() => void settings.refetch()} />;
  }
  if (!settings.data) return null;
  return <NearestCityForm settings={settings.data} />;
}

function NearestCityForm({ settings }: { settings: AdminSettings }) {
  const updateSettings = useUpdateSettings();
  const form = useForm<NearestCitySettingsValues>({
    resolver: zodResolver(nearestCitySettingsSchema),
    values: toFormValues(settings),
    resetOptions: keepUnsavedEdits,
  });
  const errors = form.formState.errors;

  return (
    <SettingsCard>
      <form
        noValidate
        className="space-y-5"
        onSubmit={form.handleSubmit((values) =>
          updateSettings.mutate(
            { version: settings.version, ...changedFields(values, form.formState.dirtyFields) },
            settingsSaveHandlers(form, toFormValues, "Saved. Use my location uses this furthest distance."),
          ),
        )}
      >
        <TextField
          label="Furthest city (km)"
          fullWidth
          inputMode="decimal"
          error={Boolean(errors.nearestCityMaxKm)}
          helperText={errors.nearestCityMaxKm?.message ?? "Use my location ignores a city farther than this."}
          {...form.register("nearestCityMaxKm")}
        />
        <Button
          type="submit"
          variant="contained"
          disabled={updateSettings.isPending || !form.formState.isDirty}
          sx={{ minHeight: 44, borderRadius: "14px" }}
        >
          {updateSettings.isPending ? "Saving…" : "Save furthest city"}
        </Button>
      </form>
    </SettingsCard>
  );
}
