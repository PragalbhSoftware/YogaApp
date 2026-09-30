import { TextField } from "@mui/material";
import type { HTMLInputTypeAttribute, Ref } from "react";
import { fieldInputSx } from "@/features/profile/components/field-styles";

type AddressFieldProps = {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  inputRef?: Ref<HTMLInputElement | HTMLTextAreaElement>;
  placeholder?: string;
  autoComplete?: string;
  error?: string;
  multiline?: boolean;
  minRows?: number;
  maxLength?: number;
  inputMode?: "text" | "numeric" | "decimal";
  type?: HTMLInputTypeAttribute;
};

const fieldSx = {
  "& .MuiOutlinedInput-root": fieldInputSx,
  "& .MuiOutlinedInput-input": {
    py: 1.75,
  },
  "& .MuiInputBase-inputMultiline": {
    py: 1.5,
  },
  "& .MuiFormHelperText-root": {
    mx: 0,
    mt: 1,
    mb: 0,
  },
};

export function AddressField({
  id,
  label,
  value,
  onChange,
  onBlur,
  inputRef,
  placeholder,
  autoComplete,
  error,
  multiline = false,
  minRows = 2,
  maxLength,
  inputMode,
  type = "text",
}: AddressFieldProps) {
  return (
    <div className="flex flex-col gap-2">
      <label htmlFor={id} className="text-sm font-medium text-brand-text">
        {label}
      </label>
      <TextField
        id={id}
        value={value ?? ""}
        onChange={(event) => onChange(event.target.value)}
        onInput={(event) => onChange((event.target as HTMLInputElement | HTMLTextAreaElement).value)}
        onBlur={onBlur}
        inputRef={inputRef}
        hiddenLabel
        fullWidth
        type={type}
        placeholder={placeholder}
        autoComplete={autoComplete}
        multiline={multiline}
        minRows={multiline ? minRows : undefined}
        error={Boolean(error)}
        helperText={error}
        slotProps={{
          htmlInput: {
            maxLength,
            inputMode,
          },
        }}
        sx={fieldSx}
      />
    </div>
  );
}
