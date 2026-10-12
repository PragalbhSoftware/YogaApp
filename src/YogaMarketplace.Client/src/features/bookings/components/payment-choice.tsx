import { FormControlLabel, Radio, RadioGroup } from "@mui/material";
import type { BookingQuote } from "@/features/bookings/types";
import type { PayMethod } from "@/features/bookings/utils/pay-method";
import { formatSlotDay } from "@/utils/clock";
import { formatInr } from "@/utils/money";

type PaymentChoiceProps = {
  quote: BookingQuote;
  method: PayMethod;
  disabled: boolean;
  onChange: (method: PayMethod) => void;
};

/** "Use 1 credit" or "Pay ₹X", both priced by the server quote. */
export function PaymentChoice({ quote, method, disabled, onChange }: PaymentChoiceProps) {
  const { credit } = quote;
  const creditLabel =
    credit.total > 0 ? `Use 1 credit + ${formatInr(credit.total)} convenience fee` : "Use 1 credit";

  return (
    <fieldset className="space-y-2 rounded-3xl border border-brand-border bg-brand-surface p-5">
      <legend className="px-1 font-heading text-base font-medium">How to pay</legend>
      <RadioGroup value={method} onChange={(_, value) => onChange(value as PayMethod)}>
        <FormControlLabel
          value="credit"
          disabled={disabled || !credit.available}
          control={<Radio />}
          label={creditLabel}
        />
        <FormControlLabel
          value="pay"
          disabled={disabled}
          control={<Radio />}
          label={`Pay ${formatInr(quote.total)}`}
        />
      </RadioGroup>
      <p className="text-sm text-brand-muted">
        {credit.available
          ? `From ${credit.packName ?? "your pack"}${credit.useBy ? `, use by ${formatSlotDay(credit.useBy)}` : ""}.`
          : credit.message}
      </p>
    </fieldset>
  );
}
