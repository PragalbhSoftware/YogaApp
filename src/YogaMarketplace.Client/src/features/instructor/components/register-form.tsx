import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, MenuItem, TextField } from "@mui/material";
import { AddressField } from "@/features/profile/components/address-field";
import { SessionModesFieldset } from "@/features/instructor/components/session-modes-fieldset";
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
  /** `split` puts the details and the session modes side by side on large screens. */
  layout?: "stacked" | "split";
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
  layout = "stacked",
  onSubmit,
}: RegisterFormProps) {
  const split = layout === "split";
  const form = useForm<InstructorRegisterFormValues>({
    resolver: zodResolver(instructorRegisterSchema),
    defaultValues,
  });

  return (
    <form
      className="space-y-6 rounded-3xl border border-brand-border bg-brand-surface p-5 sm:p-6"
      onSubmit={form.handleSubmit((values) => onSubmit(toRegisterPayload(values)))}
      noValidate
    >
      <div className={split ? "grid gap-6 lg:grid-cols-2 lg:gap-8" : "space-y-6"}>
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
                      {area.name} · {area.cityName}
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

        <SessionModesFieldset form={form} disabled={busy} />
      </div>

      <div className={split ? "flex justify-end" : undefined}>
        <Button
          type="submit"
          variant="contained"
          fullWidth
          disabled={busy || form.formState.isSubmitting}
          sx={{ minHeight: 44, borderRadius: "14px", ...(split ? { width: { lg: "auto" }, px: { lg: 5 } } : {}) }}
        >
          {busy ? busyLabel : submitLabel}
        </Button>
      </div>
    </form>
  );
}
