import type { ReactNode } from "react";
import { ButtonBase } from "@mui/material";
import { Building2, Check, House, Video, type LucideIcon } from "lucide-react";
import { Controller, useWatch, type UseFormReturn } from "react-hook-form";
import { AddressField } from "@/features/profile/components/address-field";
import type { InstructorRegisterFormValues } from "@/features/instructor/schemas";
import { formatInr } from "@/utils/money";

type FormValues = InstructorRegisterFormValues;
type ToggleName = "offersHome" | "offersStudio" | "offersOnline";
type RateName = "homeRate" | "studioRate" | "onlineRate";

type ModeConfig = {
  title: string;
  hint: string;
  icon: LucideIcon;
  toggle: ToggleName;
  rate: RateName;
  ratePlaceholder: string;
};

const modes: ModeConfig[] = [
  {
    title: "Home",
    hint: "You visit the student",
    icon: House,
    toggle: "offersHome",
    rate: "homeRate",
    ratePlaceholder: "899",
  },
  {
    title: "Studio",
    hint: "Students come to you",
    icon: Building2,
    toggle: "offersStudio",
    rate: "studioRate",
    ratePlaceholder: "749",
  },
  {
    title: "Online",
    hint: "On Google Meet",
    icon: Video,
    toggle: "offersOnline",
    rate: "onlineRate",
    ratePlaceholder: "599",
  },
];

type SessionModesFieldsetProps = {
  form: UseFormReturn<FormValues>;
  disabled: boolean;
};

export function SessionModesFieldset({ form, disabled }: SessionModesFieldsetProps) {
  const values = useWatch({
    control: form.control,
    name: ["offersHome", "offersStudio", "offersOnline", "homeRate", "studioRate", "onlineRate"],
  });
  const [offersHome, offersStudio, offersOnline] = values;
  const enabled: Record<ToggleName, boolean> = { offersHome, offersStudio, offersOnline };
  const rates: Record<RateName, string> = { homeRate: values[3], studioRate: values[4], onlineRate: values[5] };
  const active = modes.filter((mode) => enabled[mode.toggle]);
  const error = form.formState.errors.offersHome?.message;

  return (
    <fieldset>
      <legend className="mb-1 font-heading text-lg font-medium">Session modes</legend>
      <p className="mb-4 text-sm leading-relaxed text-brand-muted">Choose how you teach, then set a price for each.</p>

      <div className="space-y-4">
        <div className="grid grid-cols-3 gap-3">
          {modes.map((mode) => (
            <ModeToggle
              key={mode.toggle}
              mode={mode}
              checked={enabled[mode.toggle]}
              rate={rates[mode.rate]}
              disabled={disabled}
              onToggle={(checked) => form.setValue(mode.toggle, checked, { shouldValidate: true, shouldDirty: true })}
            />
          ))}
        </div>

        {error ? (
          <p className="text-sm text-red-700" role="alert">
            {error}
          </p>
        ) : null}

        {active.length === 0 ? (
          <p className="rounded-2xl border border-dashed border-brand-border px-4 py-6 text-center text-sm text-brand-muted">
            Pick at least one mode to set its price.
          </p>
        ) : (
          <div className="space-y-3">
            {active.map((mode) => (
              <ModePanel key={mode.toggle} mode={mode}>
                <RateField form={form} mode={mode} />
                {mode.toggle === "offersStudio" ? <StudioAddressField form={form} /> : null}
                {mode.toggle === "offersOnline" ? <MeetLinkField form={form} /> : null}
              </ModePanel>
            ))}
          </div>
        )}
      </div>
    </fieldset>
  );
}

type ModeToggleProps = {
  mode: ModeConfig;
  checked: boolean;
  rate: string;
  disabled: boolean;
  onToggle: (checked: boolean) => void;
};

function ModeToggle({ mode, checked, rate, disabled, onToggle }: ModeToggleProps) {
  const Icon = mode.icon;
  const price = Number(rate);
  const summary = !checked ? "Off" : price > 0 ? formatInr(price) : "Set a price";

  return (
    <ButtonBase
      role="switch"
      aria-checked={checked}
      aria-label={`Offer ${mode.title} sessions`}
      disabled={disabled}
      onClick={() => onToggle(!checked)}
      focusRipple
      sx={{ borderRadius: "16px", display: "block", textAlign: "left" }}
      className={`relative w-full rounded-2xl border p-3 transition-colors sm:p-4 ${
        checked
          ? "border-brand-primary bg-brand-primary/10"
          : "border-brand-border bg-brand-surface hover:border-brand-primary/50"
      }`}
    >
      <span
        aria-hidden="true"
        className={`absolute top-3 right-3 flex size-5 items-center justify-center rounded-full border ${
          checked ? "border-brand-primary bg-brand-primary text-white" : "border-brand-border bg-brand-surface"
        }`}
      >
        {checked ? <Check className="size-3.5" /> : null}
      </span>
      <Icon className={`size-6 ${checked ? "text-brand-primary" : "text-brand-muted"}`} aria-hidden="true" />
      <span className="mt-3 block text-sm font-semibold text-brand-text">{mode.title}</span>
      <span className="mt-0.5 hidden text-xs text-brand-muted sm:block">{mode.hint}</span>
      <span className={`mt-2 block text-xs font-medium ${checked ? "text-brand-primary" : "text-brand-muted"}`}>
        {summary}
      </span>
    </ButtonBase>
  );
}

function ModePanel({ mode, children }: { mode: ModeConfig; children: ReactNode }) {
  const Icon = mode.icon;
  return (
    <section
      aria-label={`${mode.title} sessions`}
      className="space-y-4 rounded-2xl border border-brand-border bg-brand-background p-4"
    >
      <div className="flex items-center gap-2">
        <Icon className="size-4 text-brand-primary" aria-hidden="true" />
        <h3 className="text-sm font-semibold text-brand-text">{mode.title}</h3>
        <span className="text-xs text-brand-muted">· {mode.hint}</span>
      </div>
      <div className="grid gap-4 sm:grid-cols-[9rem_minmax(0,1fr)]">{children}</div>
    </section>
  );
}

type FieldProps = { form: UseFormReturn<FormValues> };

function RateField({ form, mode }: FieldProps & { mode: ModeConfig }) {
  return (
    <Controller
      name={mode.rate}
      control={form.control}
      render={({ field, fieldState }) => (
        <AddressField
          id={`instructor-${mode.title.toLowerCase()}-rate`}
          label="Price per session"
          prefix="₹"
          placeholder={mode.ratePlaceholder}
          inputMode="decimal"
          value={field.value}
          onChange={field.onChange}
          onBlur={field.onBlur}
          inputRef={field.ref}
          error={fieldState.error?.message}
        />
      )}
    />
  );
}

function StudioAddressField({ form }: FieldProps) {
  return (
    <Controller
      name="studioAddress"
      control={form.control}
      render={({ field, fieldState }) => (
        <AddressField
          id="instructor-studio-address"
          label="Studio address"
          placeholder="Studio name, street, area"
          maxLength={300}
          value={field.value}
          onChange={field.onChange}
          onBlur={field.onBlur}
          inputRef={field.ref}
          error={fieldState.error?.message}
        />
      )}
    />
  );
}

function MeetLinkField({ form }: FieldProps) {
  return (
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
  );
}
