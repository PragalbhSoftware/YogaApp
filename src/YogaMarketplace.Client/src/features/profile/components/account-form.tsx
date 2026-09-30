import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, FormControl, FormHelperText, MenuItem, Select } from "@mui/material";
import { UserRound } from "lucide-react";
import { AddressField } from "@/features/profile/components/address-field";
import { fieldInputSx } from "@/features/profile/components/field-styles";
import {
  customerAccountSchema,
  toAccountPayload,
  type CustomerAccountFormValues,
} from "@/features/profile/schemas";
import type { UpdateCustomerAccountInput } from "@/features/profile/types";

type AccountFormProps = {
  defaultName: string;
  defaultGender: string | null;
  busy: boolean;
  onSubmit: (input: UpdateCustomerAccountInput) => void;
};

export function AccountForm({ defaultName, defaultGender, busy, onSubmit }: AccountFormProps) {
  const form = useForm<CustomerAccountFormValues>({
    resolver: zodResolver(customerAccountSchema),
    values: {
      name: defaultName,
      gender: defaultGender === "Female" || defaultGender === "Male" || defaultGender === "Other" ? defaultGender : "",
    },
  });

  return (
    <form
      className="flex flex-col gap-6 rounded-3xl border border-brand-border bg-brand-surface p-5 sm:p-6"
      onSubmit={form.handleSubmit((values) => onSubmit(toAccountPayload(values)))}
      noValidate
    >
      <div className="flex items-start gap-3">
        <span
          className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-brand-primary/10 text-brand-primary"
          aria-hidden="true"
        >
          <UserRound className="size-5" />
        </span>
        <div className="min-w-0">
          <h2 className="font-heading text-lg font-medium">Your details</h2>
          <p className="mt-1 text-sm leading-relaxed text-brand-muted">
            This name is used on your bookings. Phone stays the same.
          </p>
        </div>
      </div>

      <div className="flex flex-col gap-5">
        <Controller
          name="name"
          control={form.control}
          render={({ field, fieldState }) => (
            <AddressField
              id="customer-name"
              label="Name"
              placeholder="Your name"
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
        <Controller
          name="gender"
          control={form.control}
          render={({ field, fieldState }) => (
            <div className="flex flex-col gap-2">
              <label id="customer-gender-label" className="text-sm font-medium text-brand-text">
                Gender
              </label>
              <FormControl fullWidth error={Boolean(fieldState.error)}>
                <Select
                  {...field}
                  labelId="customer-gender-label"
                  value={field.value ?? ""}
                  displayEmpty
                  sx={fieldInputSx}
                >
                  <MenuItem value="">
                    <span className="text-brand-muted">Select</span>
                  </MenuItem>
                  <MenuItem value="Female">Female</MenuItem>
                  <MenuItem value="Male">Male</MenuItem>
                  <MenuItem value="Other">Other</MenuItem>
                </Select>
                {fieldState.error ? (
                  <FormHelperText sx={{ mx: 0, mt: 1 }}>{fieldState.error.message}</FormHelperText>
                ) : null}
              </FormControl>
            </div>
          )}
        />
      </div>

      <Button
        type="submit"
        variant="contained"
        fullWidth
        disabled={busy || form.formState.isSubmitting || !form.formState.isDirty}
        sx={{ minHeight: 48, borderRadius: "14px" }}
      >
        {busy ? "Saving…" : "Save details"}
      </Button>
    </form>
  );
}
