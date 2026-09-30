import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Card, CardContent, Typography } from "@mui/material";
import { AddressField } from "@/features/profile/components/address-field";
import {
  instructorRatesSchema,
  ratesFormFromProfile,
  toRatesPayload,
  type InstructorRatesFormValues,
} from "@/features/instructor/schemas";
import type { InstructorProfile, UpdateInstructorRatesInput } from "@/features/instructor/types";

type RatesFormProps = {
  profile: InstructorProfile;
  busy: boolean;
  onSubmit: (input: UpdateInstructorRatesInput) => void;
};

export function RatesForm({ profile, busy, onSubmit }: RatesFormProps) {
  const form = useForm<InstructorRatesFormValues>({
    resolver: zodResolver(instructorRatesSchema(profile)),
    values: ratesFormFromProfile(profile),
  });

  const offered = profile.offersHome || profile.offersStudio || profile.offersOnline;

  return (
    <Card elevation={0} sx={{ borderRadius: 4, border: "1px solid", borderColor: "divider" }}>
      <CardContent>
        <form
          className="space-y-5"
          onSubmit={form.handleSubmit((values) => onSubmit(toRatesPayload(profile, values)))}
        >
          <div className="space-y-2">
            <Typography variant="h6">Session prices</Typography>
            <p className="text-sm leading-relaxed text-brand-muted">
              Customers pay this when they book. Sessions already paid keep the amount they paid.
            </p>
          </div>

          {offered ? (
            <div className="space-y-4">
              {profile.offersHome ? (
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
              {profile.offersStudio ? (
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
              ) : null}
              {profile.offersOnline ? (
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
              ) : null}
            </div>
          ) : (
            <p className="text-sm leading-relaxed text-brand-muted">
              No session modes are on this profile yet, so there is no price to update.
            </p>
          )}

          {offered ? (
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={busy || form.formState.isSubmitting || !form.formState.isDirty}
              sx={{ minHeight: 44, borderRadius: "14px" }}
            >
              {busy ? "Saving…" : "Save prices"}
            </Button>
          ) : null}
        </form>
      </CardContent>
    </Card>
  );
}
