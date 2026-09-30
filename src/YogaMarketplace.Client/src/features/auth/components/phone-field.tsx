import { InputAdornment, TextField } from "@mui/material";
import { Phone } from "lucide-react";
import { digitsOnly, formatMobileDisplay } from "@/features/auth/utils/phone";

type PhoneFieldProps = {
  id?: string;
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  error?: boolean;
  helperText?: string;
};

export function PhoneField({ id = "phone", value, onChange, onBlur, error, helperText }: PhoneFieldProps) {
  return (
    <TextField
      id={id}
      label="Mobile number"
      value={formatMobileDisplay(value)}
      onChange={(event) => onChange(digitsOnly(event.target.value))}
      onBlur={onBlur}
      autoComplete="tel"
      placeholder="98765 43210"
      fullWidth
      error={error}
      helperText={helperText ?? "10-digit Indian mobile"}
      slotProps={{
        htmlInput: { inputMode: "tel", maxLength: 11, "aria-label": "Mobile number" },
        input: {
          startAdornment: (
            <InputAdornment position="start">
              <span className="flex items-center gap-2 pr-2 text-brand-text">
                <Phone className="size-4 text-brand-primary" aria-hidden="true" />
                <span className="font-medium">+91</span>
                <span className="h-6 w-px bg-brand-border" aria-hidden="true" />
              </span>
            </InputAdornment>
          ),
        },
      }}
      sx={{
        "& .MuiOutlinedInput-root": { minHeight: 56, borderRadius: "14px" },
        "& .MuiOutlinedInput-input": { py: 1.75, letterSpacing: "0.04em" },
      }}
    />
  );
}
