import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, FormControlLabel, MenuItem, Switch, TextField } from "@mui/material";
import type { ReactNode } from "react";
import { AddressField } from "@/features/profile/components/address-field";
import {
  instructorRegisterSchema,
  toRegisterPayload,
  type InstructorRegisterFormValues,
} from "@/features/instructor/schemas";
import type { RegisterInstructorInput } from "@/features/instructor/types";
import type { Area } from "@/features/marketplace/types";
import { brand } from "@/constants/brand";

type RegisterFormProps = {
  areas: Area[];
  defaultValues: InstructorRegisterFormValues;
  busy: boolean;
  submitLabel?: string;
  busyLabel?: string;
  onSubmit: (input: RegisterInstructorInput) => void;
};

const selectSx = {
  "& .MuiOutlinedInput-root": {
    minHeight: 52,
    borderRadius: "16px",
    backgroundColor: brand.background,
  },
  "& .MuiFormHelperText-root": {
    mx: 0,
    mt: 1,
    mb: 0,
  },
};

export function RegisterForm({
  areas,
  defaultValues,
  busy,
  submitLabel = "Submit for review",
  busyLabel = "Submitting…",
  onSubmit,
}: RegisterFormProps) {
  const form = useForm<InstructorRegisterFormValues>({
    resolver: zodResolver(instructorRegisterSchema),
    defaultValues,
  });
  const offersHome = form.watch("offersHome");
  const offersStudio = form.watch("offersStudio");
  const offersOnline = form.watch("offersOnline");

  return (
    <form
      className="space-y-6 rounded-3xl border border-brand-border bg-brand-surface p-5 sm:p-6"
      onSubmit={form.handleSubmit((values) => onSubmit(toRegisterPayload(values)))}
      noValidate
    >
      <div className="space-y-5">
        <Controller
          name="displayName"
          control={form.control}
          render={({ field, fieldState }) => (
            <AddressField
              id="instructor-display-name"
              label="Display name"
              placeholder="How students will see you"
              autoComplete="name"
              maxLength={80}
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
            name="age"
            control={form.control}
            render={({ field, fieldState }) => (
              <AddressField
                id="instructor-age"
                label="Age (optional)"
                placeholder="18–80"
                inputMode="numeric"
                maxLength={2}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                inputRef={field.ref}
                error={fieldState.error?.message}
              />
            )}
          />
          <Controller
            name="email"
            control={form.control}
            render={({ field, fieldState }) => (
              <AddressField
                id="instructor-email"
                label="Email (optional)"
                placeholder="you@example.com"
                type="email"
                autoComplete="email"
                maxLength={200}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                inputRef={field.ref}
                error={fieldState.error?.message}
              />
            )}
          />
        </div>
        <Controller
          name="areaId"
          control={form.control}
          render={({ field, fieldState }) => (
            <div className="flex flex-col gap-2">
              <label htmlFor="instructor-area" className="text-sm font-medium text-brand-text">
                Area
              </label>
              <TextField
                id="instructor-area"
                select
                hiddenLabel
                fullWidth
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                inputRef={field.ref}
                error={Boolean(fieldState.error)}
                helperText={fieldState.error?.message}
                sx={selectSx}
              >
                <MenuItem value="">Select an area</MenuItem>
                {areas.map((area) => (
                  <MenuItem key={area.id} value={area.id}>
                    {area.name} · {area.city}
                  </MenuItem>
                ))}
              </TextField>
            </div>
          )}
        />
        <Controller
          name="bio"
          control={form.control}
          render={({ field, fieldState }) => (
            <AddressField
              id="instructor-bio"
              label="Bio (optional)"
              placeholder="What you teach, and who it is for"
              multiline
              minRows={3}
              maxLength={1000}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
              inputRef={field.ref}
              error={fieldState.error?.message}
            />
          )}
        />
      </div>

      <fieldset className="space-y-4">
        <legend className="font-heading text-lg font-medium">Session modes</legend>
        <p className="text-sm leading-relaxed text-brand-muted">
          Turn on Home, Studio, or Online. Each mode needs a rate in INR. Studio needs an address. Online
          needs an https://meet.google.com link.
        </p>
        {form.formState.errors.offersHome?.message ? (
          <p className="text-sm text-red-700" role="alert">
            {form.formState.errors.offersHome.message}
          </p>
        ) : null}

        <ModeBlock
          title="Home"
          checked={offersHome}
          onChange={(checked) => form.setValue("offersHome", checked, { shouldValidate: true })}
          disabled={busy}
        >
          {offersHome ? (
            <Controller
              name="homeRate"
              control={form.control}
              render={({ field, fieldState }) => (
                <AddressField
                  id="instructor-home-rate"
                  label="Home rate (INR)"
                  placeholder="899"
                  inputMode="decimal"
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  inputRef={field.ref}
                  error={fieldState.error?.message}
                />
              )}
            />
          ) : null}
        </ModeBlock>

        <ModeBlock
          title="Studio"
          checked={offersStudio}
          onChange={(checked) => form.setValue("offersStudio", checked, { shouldValidate: true })}
          disabled={busy}
        >
          {offersStudio ? (
            <div className="space-y-5">
              <Controller
                name="studioRate"
                control={form.control}
                render={({ field, fieldState }) => (
                  <AddressField
                    id="instructor-studio-rate"
                    label="Studio rate (INR)"
                    placeholder="749"
                    inputMode="decimal"
                    value={field.value}
                    onChange={field.onChange}
                    onBlur={field.onBlur}
                    inputRef={field.ref}
                    error={fieldState.error?.message}
                  />
                )}
              />
              <Controller
                name="studioAddress"
                control={form.control}
                render={({ field, fieldState }) => (
                  <AddressField
                    id="instructor-studio-address"
                    label="Studio address"
                    placeholder="Studio name, street, area"
                    maxLength={300}
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
            </div>
          ) : null}
        </ModeBlock>

        <ModeBlock
          title="Online"
          checked={offersOnline}
          onChange={(checked) => form.setValue("offersOnline", checked, { shouldValidate: true })}
          disabled={busy}
        >
          {offersOnline ? (
            <div className="space-y-5">
              <Controller
                name="onlineRate"
                control={form.control}
                render={({ field, fieldState }) => (
                  <AddressField
                    id="instructor-online-rate"
                    label="Online rate (INR)"
                    placeholder="599"
                    inputMode="decimal"
                    value={field.value}
                    onChange={field.onChange}
                    onBlur={field.onBlur}
                    inputRef={field.ref}
                    error={fieldState.error?.message}
                  />
                )}
              />
              <Controller
                name="googleMeetLink"
                control={form.control}
                render={({ field, fieldState }) => (
                  <AddressField
                    id="instructor-meet-link"
                    label="Google Meet link"
                    placeholder="https://meet.google.com/abc-defg-hij"
                    type="url"
                    value={field.value}
                    onChange={field.onChange}
                    onBlur={field.onBlur}
                    inputRef={field.ref}
                    error={fieldState.error?.message}
                  />
                )}
              />
            </div>
          ) : null}
        </ModeBlock>
      </fieldset>

      <Button
        type="submit"
        variant="contained"
        fullWidth
        disabled={busy || form.formState.isSubmitting}
        sx={{ minHeight: 44, borderRadius: "14px" }}
      >
        {busy ? busyLabel : submitLabel}
      </Button>
    </form>
  );
}

function ModeBlock({
  title,
  checked,
  disabled,
  onChange,
  children,
}: {
  title: string;
  checked: boolean;
  disabled: boolean;
  onChange: (checked: boolean) => void;
  children: ReactNode;
}) {
  return (
    <div className="space-y-4 rounded-2xl bg-brand-background px-4 py-4">
      <FormControlLabel
        sx={{ m: 0, width: "100%", justifyContent: "space-between", ml: 0 }}
        labelPlacement="start"
        control={
          <Switch
            checked={checked}
            disabled={disabled}
            onChange={(event) => onChange(event.target.checked)}
            slotProps={{ input: { "aria-label": `Offer ${title} sessions` } }}
          />
        }
        label={<span className="text-sm font-medium text-brand-text">{title}</span>}
      />
      {children}
    </div>
  );
}
