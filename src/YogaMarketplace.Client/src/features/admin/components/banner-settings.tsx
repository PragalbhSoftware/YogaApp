import { Button, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useAdminSettings, useUpdateSettings } from "@/features/admin/hooks/use-admin";
import { bannerSchema, settingsLimits, type BannerValues } from "@/features/admin/schemas";
import type { AdminSettings } from "@/features/admin/types";
import { changedFields } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";

export function BannerSettings() {
  const settings = useAdminSettings();

  if (settings.isLoading) return <BookingListSkeleton />;
  if (settings.isError) {
    return <ErrorState message={toUserMessage(settings.error)} onRetry={() => void settings.refetch()} />;
  }
  if (!settings.data) return null;

  return <BannerForm settings={settings.data} />;
}

function BannerForm({ settings }: { settings: AdminSettings }) {
  const updateSettings = useUpdateSettings();
  const form = useForm<BannerValues>({
    resolver: zodResolver(bannerSchema),
    values: {
      bannerTitle: settings.bannerTitle ?? "",
      bannerSubtitle: settings.bannerSubtitle ?? "",
      bannerOffer: settings.bannerOffer ?? "",
    },
  });
  const errors = form.formState.errors;

  return (
    <div className="space-y-5">
      <SettingsIntro
        title="Home banner"
        lead="The headline customers see on the home page. Leave a field empty to show the default text."
      />
      <SettingsCard>
        <form
          noValidate
          className="space-y-4"
          onSubmit={form.handleSubmit((values) => {
            updateSettings.mutate(
              { version: settings.version, ...changedFields(values, form.formState.dirtyFields) },
              {
                onSuccess: () => toast.success("Banner saved."),
                onError: (error) => toast.error(toUserMessage(error)),
              },
            );
          })}
        >
          <TextField
            label="Title"
            fullWidth
            error={Boolean(errors.bannerTitle)}
            helperText={errors.bannerTitle?.message ?? "For example: Yoga at home, in the studio or online."}
            {...form.register("bannerTitle")}
            slotProps={{ htmlInput: { maxLength: settingsLimits.bannerTitleMax } }}
          />
          <TextField
            label="Subtitle"
            fullWidth
            multiline
            minRows={2}
            error={Boolean(errors.bannerSubtitle)}
            helperText={errors.bannerSubtitle?.message ?? "One short line under the title."}
            {...form.register("bannerSubtitle")}
            slotProps={{ htmlInput: { maxLength: settingsLimits.bannerSubtitleMax } }}
          />
          <TextField
            label="Offer"
            fullWidth
            error={Boolean(errors.bannerOffer)}
            helperText={errors.bannerOffer?.message ?? "Shown as a small chip, for example: First class 20% off."}
            {...form.register("bannerOffer")}
            slotProps={{ htmlInput: { maxLength: settingsLimits.bannerOfferMax } }}
          />
          <Button
            type="submit"
            variant="contained"
            disabled={updateSettings.isPending || !form.formState.isDirty}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {updateSettings.isPending ? "Saving…" : "Save banner"}
          </Button>
        </form>
      </SettingsCard>
    </div>
  );
}
