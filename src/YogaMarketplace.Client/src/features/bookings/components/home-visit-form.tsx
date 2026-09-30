import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@mui/material";
import { MapPin } from "lucide-react";
import {
  emptyHomeVisitForm,
  homeVisitFormSchema,
  toHomeVisitPayload,
  type HomeVisitFormValues,
  type HomeVisitValues,
} from "@/features/bookings/schemas";
import { AddressField } from "@/features/profile/components/address-field";

type HomeVisitFormProps = {
  busy: boolean;
  defaultValues?: HomeVisitFormValues;
  title?: string;
  description?: string;
  submitLabel?: string;
  busyLabel?: string;
  cancelLabel?: string;
  onCancel?: () => void;
  onSubmit: (values: HomeVisitValues, form: HomeVisitFormValues) => void;
};

export function HomeVisitForm({
  busy,
  defaultValues = emptyHomeVisitForm,
  title = "Visit address",
  description = "Home sessions need a full address so the instructor can reach you. We’ll keep it for next time.",
  submitLabel = "Pay with Razorpay",
  busyLabel = "Opening payment…",
  cancelLabel = "Use saved address",
  onCancel,
  onSubmit,
}: HomeVisitFormProps) {
  const form = useForm<HomeVisitFormValues>({
    resolver: zodResolver(homeVisitFormSchema),
    defaultValues,
  });

  return (
    <form
      className="flex flex-col gap-6 rounded-3xl border border-brand-border bg-brand-surface p-5 sm:p-6"
      onSubmit={form.handleSubmit((values) => onSubmit(toHomeVisitPayload(values), values))}
      noValidate
    >
      <div className="flex items-start gap-3">
        <span
          className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-brand-primary/10 text-brand-primary"
          aria-hidden="true"
        >
          <MapPin className="size-5" />
        </span>
        <div className="min-w-0">
          <h2 className="font-heading text-lg font-medium">{title}</h2>
          <p className="mt-1 text-sm leading-relaxed text-brand-muted">{description}</p>
        </div>
      </div>

      <div className="flex flex-col gap-5">
        <Controller
          name="line1"
          control={form.control}
          render={({ field, fieldState }) => (
            <AddressField
              id="home-line1"
              label="House and street"
              placeholder="Flat, building, street"
              autoComplete="address-line1"
              multiline
              minRows={2}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
              inputRef={field.ref}
              error={fieldState.error?.message}
            />
          )}
        />

        <Controller
          name="area"
          control={form.control}
          render={({ field, fieldState }) => (
            <AddressField
              id="home-area"
              label="Area / locality"
              placeholder="Neighbourhood"
              autoComplete="address-level3"
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
              inputRef={field.ref}
              error={fieldState.error?.message}
            />
          )}
        />

        <div className="grid gap-5 sm:grid-cols-2">
          <Controller
            name="city"
            control={form.control}
            render={({ field, fieldState }) => (
              <AddressField
                id="home-city"
                label="City"
                placeholder="City"
                autoComplete="address-level2"
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                inputRef={field.ref}
                error={fieldState.error?.message}
              />
            )}
          />
          <Controller
            name="pin"
            control={form.control}
            render={({ field, fieldState }) => (
              <AddressField
                id="home-pin"
                label="PIN code"
                placeholder="400001"
                autoComplete="postal-code"
                inputMode="numeric"
                maxLength={6}
                value={field.value}
                onChange={(value) => field.onChange(value.replace(/\D/g, "").slice(0, 6))}
                onBlur={field.onBlur}
                inputRef={field.ref}
                error={fieldState.error?.message}
              />
            )}
          />
        </div>

        <Controller
          name="landmark"
          control={form.control}
          render={({ field, fieldState }) => (
            <AddressField
              id="home-landmark"
              label="Landmark"
              placeholder="Near a well-known place"
              autoComplete="off"
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
              inputRef={field.ref}
              error={fieldState.error?.message}
            />
          )}
        />
      </div>

      <div className="flex flex-col gap-2 pt-1">
        <Button type="submit" variant="contained" fullWidth disabled={busy} sx={{ minHeight: 48, borderRadius: "14px" }}>
          {busy ? busyLabel : submitLabel}
        </Button>
        {onCancel ? (
          <Button type="button" variant="text" fullWidth disabled={busy} onClick={onCancel}>
            {cancelLabel}
          </Button>
        ) : null}
      </div>
    </form>
  );
}
