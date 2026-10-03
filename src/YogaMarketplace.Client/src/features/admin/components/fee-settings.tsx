import { Button, TextField, ToggleButton, ToggleButtonGroup } from "@mui/material";
import { Controller, useForm, useWatch, type Control } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsAudit } from "@/features/admin/components/settings-audit";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { StatCard } from "@/features/admin/components/stat-card";
import { useAdminSettings, useUpdateSettings } from "@/features/admin/hooks/use-admin";
import { feeSettingsSchema, settingsLimits, type FeeSettingsValues } from "@/features/admin/schemas";
import type { AdminSettings } from "@/features/admin/types";
import { changedFields, formatAdminDay, lateFeeLabel } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";
import { formatInr } from "@/utils/money";

export function FeeSettings() {
  const settings = useAdminSettings();

  if (settings.isLoading) return <BookingListSkeleton />;
  if (settings.isError) {
    return <ErrorState message={toUserMessage(settings.error)} onRetry={() => void settings.refetch()} />;
  }
  if (!settings.data) return null;

  return (
    <div className="space-y-5">
      <FeeForm settings={settings.data} />
      <SettingsAudit />
    </div>
  );
}

function toFormValues(settings: AdminSettings): FeeSettingsValues {
  return {
    commissionPercent: settings.commissionPercent,
    convenienceFee: settings.convenienceFee,
    cancelFreeWindowHours: settings.cancelFreeWindowHours,
    rescheduleFreeWindowHours: settings.rescheduleFreeWindowHours,
    lateCancelFeeType: settings.lateCancelFeeType,
    lateCancelFeeValue: settings.lateCancelFeeValue,
    payoutCycle: settings.payoutCycle,
    policyNote: settings.policyNote ?? "",
  };
}

function FeeForm({ settings }: { settings: AdminSettings }) {
  const updateSettings = useUpdateSettings();
  const form = useForm<FeeSettingsValues>({
    resolver: zodResolver(feeSettingsSchema),
    values: toFormValues(settings),
  });
  const errors = form.formState.errors;
  const feeType = useWatch({ control: form.control, name: "lateCancelFeeType" });

  return (
    <div className="space-y-5">
      <SettingsIntro
        title="Fees and payouts"
        lead="Changes apply to bookings made after you save. Existing bookings keep the fees they were booked with."
      />
      <div className="grid gap-3 sm:grid-cols-3">
        <StatCard label="Your commission" value={`${settings.commissionPercent}%`} hint="Of each session" />
        <StatCard
          label="Convenience fee"
          value={settings.convenienceFee > 0 ? formatInr(settings.convenienceFee) : "None"}
          hint="Added at checkout"
        />
        <StatCard
          label="Late cancel"
          value={lateFeeLabel(settings.lateCancelFeeType, settings.lateCancelFeeValue)}
          hint={`Inside ${settings.cancelFreeWindowHours} h`}
        />
      </div>
      <SettingsCard>
        <form
          noValidate
          className="space-y-5"
          onSubmit={form.handleSubmit((values) => {
            const changes = changedFields(values, form.formState.dirtyFields);
            updateSettings.mutate(
              { version: settings.version, ...changes },
              {
                onSuccess: () => toast.success("Saved. New bookings use these fees."),
                onError: (error) => toast.error(toUserMessage(error)),
              },
            );
          })}
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label="Commission (%)"
              fullWidth
              inputMode="decimal"
              error={Boolean(errors.commissionPercent)}
              helperText={errors.commissionPercent?.message ?? "Your share of each session. The instructor gets the rest."}
              {...form.register("commissionPercent")}
            />
            <TextField
              label="Convenience fee (₹)"
              fullWidth
              inputMode="decimal"
              error={Boolean(errors.convenienceFee)}
              helperText={
                errors.convenienceFee?.message ?? "Flat amount added at checkout. Refunded unless the customer cancels late."
              }
              {...form.register("convenienceFee")}
            />
            <TextField
              label="Free cancel window (hours)"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.cancelFreeWindowHours)}
              helperText={
                errors.cancelFreeWindowHours?.message ??
                `0 to ${settingsLimits.maxWindowHours} hours before the session. 0 means always free.`
              }
              {...form.register("cancelFreeWindowHours")}
            />
            <TextField
              label="Free reschedule window (hours)"
              fullWidth
              inputMode="numeric"
              error={Boolean(errors.rescheduleFreeWindowHours)}
              helperText={
                errors.rescheduleFreeWindowHours?.message ?? `0 to ${settingsLimits.maxWindowHours} hours before the session.`
              }
              {...form.register("rescheduleFreeWindowHours")}
            />
          </div>

          <fieldset className="space-y-3">
            <legend className="text-sm font-medium">Late-cancel fee</legend>
            <ChoiceField
              control={form.control}
              name="lateCancelFeeType"
              label="Late-cancel fee type"
              options={[
                { value: "Percent", label: "Percent of session" },
                { value: "Flat", label: "Flat amount" },
              ]}
            />
            <TextField
              label={feeType === "Flat" ? "Fee (₹)" : "Fee (%)"}
              fullWidth
              inputMode="decimal"
              error={Boolean(errors.lateCancelFeeValue)}
              helperText={
                errors.lateCancelFeeValue?.message ??
                "Kept when an accepted session is cancelled inside the window. Never more than the session price."
              }
              {...form.register("lateCancelFeeValue")}
            />
          </fieldset>

          <fieldset className="space-y-2">
            <legend className="text-sm font-medium">Payout cycle</legend>
            <ChoiceField
              control={form.control}
              name="payoutCycle"
              label="Payout cycle"
              options={[
                { value: "Weekly", label: "Weekly" },
                { value: "Biweekly", label: "Every 2 weeks" },
              ]}
            />
            <p className="text-sm text-brand-muted">
              Cycles run Monday to Sunday, India time. Export takes payouts from closed cycles only. This cycle
              started on {formatAdminDay(settings.payoutPeriodStart)}.
            </p>
          </fieldset>

          <TextField
            label="Internal note"
            fullWidth
            multiline
            minRows={3}
            error={Boolean(errors.policyNote)}
            helperText={errors.policyNote?.message ?? "For you. Customers do not see this."}
            {...form.register("policyNote")}
            slotProps={{ htmlInput: { maxLength: settingsLimits.policyNoteMax } }}
          />
          <Button
            type="submit"
            variant="contained"
            disabled={updateSettings.isPending || !form.formState.isDirty}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {updateSettings.isPending ? "Saving…" : "Save fees"}
          </Button>
        </form>
      </SettingsCard>
    </div>
  );
}

type ChoiceFieldProps = {
  control: Control<FeeSettingsValues>;
  name: "lateCancelFeeType" | "payoutCycle";
  label: string;
  options: { value: string; label: string }[];
};

function ChoiceField({ control, name, label, options }: ChoiceFieldProps) {
  return (
    <Controller
      name={name}
      control={control}
      render={({ field }) => (
        <ToggleButtonGroup
          exclusive
          value={field.value}
          onChange={(_, next: string | null) => {
            if (next) field.onChange(next);
          }}
          aria-label={label}
          sx={{ flexWrap: "wrap", "& .MuiToggleButton-root": { textTransform: "none", minHeight: 44, px: 2 } }}
        >
          {options.map((option) => (
            <ToggleButton key={option.value} value={option.value}>
              {option.label}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
      )}
    />
  );
}
