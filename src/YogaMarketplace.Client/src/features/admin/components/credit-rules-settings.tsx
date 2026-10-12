import { Button, FormControlLabel, MenuItem, Switch, TextField } from "@mui/material";
import { Controller, useForm, type Control } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useAdminSettings, useUpdateSettings } from "@/features/admin/hooks/use-admin";
import { creditSettingsSchema, type CreditSettingsValues } from "@/features/admin/schemas";
import { lateCancelOptions, payBasisOptions } from "@/features/admin/credit-labels";
import type { AdminSettings } from "@/features/admin/types";
import { changedFields, settingsSaveError } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";

type SwitchName =
  | "creditConvenienceFee"
  | "creditBookingStandsAfterExpiry"
  | "packRefundsEnabled";

function toFormValues(settings: AdminSettings): CreditSettingsValues {
  return {
    creditConvenienceFee: settings.creditConvenienceFee,
    creditPayBasis: settings.creditPayBasis,
    creditLateCancel: settings.creditLateCancel,
    creditBookingStandsAfterExpiry: settings.creditBookingStandsAfterExpiry,
    packRefundsEnabled: settings.packRefundsEnabled,
    packReferenceSessionPrice: settings.packReferenceSessionPrice,
    packRefundFee: settings.packRefundFee,
  };
}

export function CreditRulesSettings() {
  const settings = useAdminSettings();

  if (settings.isLoading) return <BookingListSkeleton />;
  if (settings.isError) {
    return <ErrorState message={toUserMessage(settings.error)} onRetry={() => void settings.refetch()} />;
  }
  if (!settings.data) return null;
  return <CreditRulesForm settings={settings.data} />;
}

function CreditRulesForm({ settings }: { settings: AdminSettings }) {
  const updateSettings = useUpdateSettings();
  const form = useForm<CreditSettingsValues>({
    resolver: zodResolver(creditSettingsSchema),
    values: toFormValues(settings),
  });
  const errors = form.formState.errors;

  return (
    <div className="space-y-5">
      <SettingsIntro
        title="Credit rules"
        lead="Applies to new purchases only. Each purchase keeps the rules in force when it was bought."
      />
      <SettingsCard>
        <form
          noValidate
          className="space-y-5"
          onSubmit={form.handleSubmit((values) =>
            updateSettings.mutate(
              { version: settings.version, ...changedFields(values, form.formState.dirtyFields) },
              {
                onSuccess: () => toast.success("Saved. New pack purchases use these rules."),
                onError: (error) => toast.error(settingsSaveError(error)),
              },
            ),
          )}
        >
          <SwitchField
            control={form.control}
            name="creditConvenienceFee"
            label="Charge the convenience fee on credit bookings"
            hint="When on, the customer pays only the fee through Razorpay to book with a credit."
          />
          <SwitchField
            control={form.control}
            name="creditBookingStandsAfterExpiry"
            label="Credit bookings stand if credits expire before the session"
            hint="When off, customers can’t use a credit for a session after the pack’s last day."
          />

          <div className="grid gap-4 sm:grid-cols-2">
            <SelectField
              control={form.control}
              name="creditPayBasis"
              label="Instructor is paid on"
              options={payBasisOptions}
              hint="Commission is taken from this amount, as for paid bookings."
            />
            <SelectField
              control={form.control}
              name="creditLateCancel"
              label="Late cancel of a credit booking"
              options={lateCancelOptions}
              hint="Early cancels always return the credit."
            />
          </div>

          <fieldset className="space-y-3">
            <legend className="text-sm font-medium">Partial refunds (admin only)</legend>
            <SwitchField
              control={form.control}
              name="packRefundsEnabled"
              label="Allow refunding unused credits"
              hint="Refund = unused credits × reference price − refund fee, capped at what the customer paid."
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField
                label="Reference session price (₹)"
                fullWidth
                inputMode="decimal"
                error={Boolean(errors.packReferenceSessionPrice)}
                helperText={errors.packReferenceSessionPrice?.message ?? "What one unused credit is worth in a refund."}
                {...form.register("packReferenceSessionPrice")}
              />
              <TextField
                label="Refund fee (₹)"
                fullWidth
                inputMode="decimal"
                error={Boolean(errors.packRefundFee)}
                helperText={errors.packRefundFee?.message ?? "Taken off each pack refund."}
                {...form.register("packRefundFee")}
              />
            </div>
          </fieldset>

          <Button
            type="submit"
            variant="contained"
            disabled={updateSettings.isPending || !form.formState.isDirty}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {updateSettings.isPending ? "Saving…" : "Save credit rules"}
          </Button>
        </form>
      </SettingsCard>
    </div>
  );
}

type SwitchFieldProps = {
  control: Control<CreditSettingsValues>;
  name: SwitchName;
  label: string;
  hint: string;
};

function SwitchField({ control, name, label, hint }: SwitchFieldProps) {
  return (
    <Controller
      name={name}
      control={control}
      render={({ field }) => (
        <div>
          <FormControlLabel
            sx={{ ml: 0 }}
            control={<Switch checked={field.value} onChange={(_, checked) => field.onChange(checked)} />}
            label={label}
          />
          <p className="text-sm text-brand-muted">{hint}</p>
        </div>
      )}
    />
  );
}

type SelectFieldProps = {
  control: Control<CreditSettingsValues>;
  name: "creditPayBasis" | "creditLateCancel";
  label: string;
  hint: string;
  options: { value: string; label: string }[];
};

function SelectField({ control, name, label, hint, options }: SelectFieldProps) {
  return (
    <Controller
      name={name}
      control={control}
      render={({ field }) => (
        <TextField select label={label} fullWidth value={field.value} onChange={field.onChange} helperText={hint}>
          {options.map((option) => (
            <MenuItem key={option.value} value={option.value}>
              {option.label}
            </MenuItem>
          ))}
        </TextField>
      )}
    />
  );
}
