import { FormControlLabel, Radio, RadioGroup } from "@mui/material";
import { trialPriceLabel } from "@/features/marketplace/utils/trial";

type TrialChoiceProps = {
  trialPrice: number;
  useTrial: boolean;
  disabled: boolean;
  onChange: (useTrial: boolean) => void;
};

export function TrialChoice({ trialPrice, useTrial, disabled, onChange }: TrialChoiceProps) {
  return (
    <fieldset className="space-y-2 rounded-3xl border border-brand-border bg-brand-surface p-5">
      <legend className="px-1 font-heading text-base font-medium">Session type</legend>
      <RadioGroup value={useTrial ? "trial" : "regular"} onChange={(_, value) => onChange(value === "trial")}>
        <FormControlLabel
          value="trial"
          disabled={disabled}
          control={<Radio />}
          label={`Trial class · ${trialPriceLabel(trialPrice)}`}
        />
        <FormControlLabel value="regular" disabled={disabled} control={<Radio />} label="Regular session" />
      </RadioGroup>
      <p className="text-sm text-brand-muted">
        One trial per instructor. Cancelling a trial doesn’t give it back.
      </p>
    </fieldset>
  );
}
