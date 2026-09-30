import { Controller, type UseFormReturn } from "react-hook-form";
import {
  Alert,
  Button,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Tab,
  Tabs,
  TextField,
} from "@mui/material";
import { PhoneField } from "@/features/auth/components/phone-field";
import type { AccountFormValues } from "@/features/auth/schemas";

type AccountStepProps = {
  form: UseFormReturn<AccountFormValues>;
  error: string | null;
  busy: boolean;
  onSubmit: (values: AccountFormValues) => void;
};

const showDevHint = import.meta.env.DEV;

export function AccountStep({ form, error, busy, onSubmit }: AccountStepProps) {
  const accountKind = form.watch("accountKind");
  const isNew = accountKind === "new";

  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h2 className="font-heading text-2xl font-medium sm:text-3xl">Account</h2>
        <p className="text-sm leading-relaxed text-brand-muted">
          New customers add a name and gender. If you already have an account, your phone
          is enough.
        </p>
      </header>

      {showDevHint ? (
        <p className="rounded-xl bg-brand-secondary/20 px-3 py-2 text-xs leading-relaxed text-brand-muted">
          Local demo: Development API uses code{" "}
          <span className="font-medium text-brand-text">123456</span>. Instructor{" "}
          <span className="font-medium text-brand-text">98765 43210</span>. Admin{" "}
          <span className="font-medium text-brand-text">90000 00001</span>.
        </p>
      ) : null}

      {error ? <Alert severity="error">{error}</Alert> : null}

      <form className="space-y-7" onSubmit={form.handleSubmit(onSubmit)} noValidate>
        <Tabs
          value={accountKind}
          onChange={(_, value: AccountFormValues["accountKind"]) =>
            form.setValue("accountKind", value)
          }
          variant="fullWidth"
          sx={{
            minHeight: 44,
            "& .MuiTab-root": {
              fontSize: { xs: 13, md: 14 },
              px: { xs: 1, md: 2 },
            },
          }}
        >
          <Tab
            value="existing"
            label={
              <>
                <span className="md:hidden">Sign in</span>
                <span className="hidden md:inline">I already have an account</span>
              </>
            }
          />
          <Tab value="new" label="I'm new" />
        </Tabs>

        {isNew ? (
          <div className="grid gap-4 md:grid-cols-2">
            <Controller
              name="name"
              control={form.control}
              render={({ field, fieldState }) => (
                <TextField
                  {...field}
                  value={field.value ?? ""}
                  id="name"
                  label="Name"
                  autoComplete="name"
                  slotProps={{ htmlInput: { maxLength: 80 } }}
                  error={Boolean(fieldState.error)}
                  helperText={fieldState.error?.message}
                />
              )}
            />
            <Controller
              name="gender"
              control={form.control}
              render={({ field, fieldState }) => (
                <FormControl fullWidth error={Boolean(fieldState.error)}>
                  <InputLabel id="gender-label">Gender</InputLabel>
                  <Select
                    {...field}
                    labelId="gender-label"
                    label="Gender"
                    value={field.value ?? ""}
                  >
                    <MenuItem value="Female">Female</MenuItem>
                    <MenuItem value="Male">Male</MenuItem>
                    <MenuItem value="Other">Other</MenuItem>
                  </Select>
                  {fieldState.error ? (
                    <FormHelperText>{fieldState.error.message}</FormHelperText>
                  ) : null}
                </FormControl>
              )}
            />
          </div>
        ) : null}

        <Controller
          name="phone"
          control={form.control}
          render={({ field, fieldState }) => (
            <PhoneField
              id="phone"
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
              error={Boolean(fieldState.error)}
              helperText={fieldState.error?.message}
            />
          )}
        />

        <div className="pt-2">
          <Button
            type="submit"
            variant="contained"
            size="large"
            fullWidth
            disabled={busy}
            sx={{ minHeight: 52, borderRadius: "14px" }}
          >
            {busy ? "Sending code…" : "Send code"}
          </Button>
        </div>
      </form>
    </div>
  );
}
