import { Button, FormControlLabel, MenuItem, Switch, TextField } from "@mui/material";
import { Controller, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { useInstructorTrial, useUpdateInstructorTrial } from "@/features/instructor/hooks/use-instructor-schedule";
import {
  toTrialPayload,
  trialFormFromSettings,
  trialSchema,
  type TrialFormValues,
} from "@/features/instructor/trial-schema";
import type { ProviderService, ProviderTrial } from "@/features/instructor/types";
import { toUserMessage } from "@/services/http/api-error";
import { formatInr } from "@/utils/money";

export function TrialSettings({ services }: { services: ProviderService[] }) {
  const trial = useInstructorTrial();

  return (
    <section className="space-y-3">
      <div className="space-y-1">
        <h2 className="font-heading text-lg font-medium">Trial class</h2>
        <p className="text-sm leading-relaxed text-brand-muted">
          Offer each new customer one low-priced or free first session. A customer who cancels or doesn’t show up
          doesn’t get another; if you decline, they can try again. Trials can’t be booked with pack credits.
        </p>
      </div>
      {trial.isLoading ? <BookingListSkeleton /> : null}
      {trial.isError ? <ErrorState message={toUserMessage(trial.error)} onRetry={() => void trial.refetch()} /> : null}
      {trial.data ? (
        <TrialForm
          key={`${trial.data.serviceId}-${trial.data.enabled}-${trial.data.price}`}
          trial={trial.data}
          services={services.filter((service) => service.isActive)}
        />
      ) : null}
    </section>
  );
}

function TrialForm({ trial, services }: { trial: ProviderTrial; services: ProviderService[] }) {
  const update = useUpdateInstructorTrial();
  const form = useForm<TrialFormValues>({
    resolver: zodResolver(trialSchema),
    defaultValues: trialFormFromSettings(trial, services),
  });
  const enabled = useWatch({ control: form.control, name: "enabled" });

  return (
    <form
      className="space-y-3 rounded-3xl border border-brand-border bg-brand-surface p-5"
      noValidate
      onSubmit={form.handleSubmit((values) =>
        update.mutate(toTrialPayload(values), {
          onSuccess: (saved) => toast.success(saved.enabled ? "Trial class saved." : "Trial class turned off."),
          onError: (error) => toast.error(toUserMessage(error)),
        }),
      )}
    >
      <Controller
        control={form.control}
        name="enabled"
        render={({ field }) => (
          <FormControlLabel
            control={<Switch checked={field.value} onChange={(_, checked) => field.onChange(checked)} />}
            label="Offer a trial class"
          />
        )}
      />
      {enabled ? (
        <>
          <Controller
            control={form.control}
            name="serviceId"
            render={({ field }) => (
              <TextField
                select
                label="Service"
                fullWidth
                value={field.value}
                onChange={field.onChange}
                error={Boolean(form.formState.errors.serviceId)}
                helperText={form.formState.errors.serviceId?.message ?? "The trial is a first session of this service."}
              >
                {services.map((service) => (
                  <MenuItem key={service.id} value={service.id}>
                    {service.title}
                  </MenuItem>
                ))}
              </TextField>
            )}
          />
          <TextField
            label="Trial price (₹)"
            fullWidth
            error={Boolean(form.formState.errors.price)}
            helperText={form.formState.errors.price?.message ?? trialHelp(trial)}
            slotProps={{ htmlInput: { inputMode: "decimal" } }}
            {...form.register("price")}
          />
        </>
      ) : null}
      <Button
        type="submit"
        variant="contained"
        disabled={update.isPending}
        sx={{ minHeight: 44, borderRadius: "14px" }}
      >
        {update.isPending ? "Saving…" : "Save trial class"}
      </Button>
    </form>
  );
}

function trialHelp(trial: ProviderTrial) {
  const ceiling = trial.lowestRegularRate == null ? "" : ` Up to ${formatInr(trial.lowestRegularRate)}.`;
  const fee =
    trial.convenienceFee > 0
      ? ` Customers also pay the ${formatInr(trial.convenienceFee)} convenience fee.`
      : " Set at least ₹1 while there is no convenience fee.";
  return `0 makes it free.${ceiling}${fee}`;
}
