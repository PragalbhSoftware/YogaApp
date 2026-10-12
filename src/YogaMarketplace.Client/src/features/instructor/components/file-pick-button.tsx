import { useRef } from "react";
import { Button } from "@mui/material";
import { Upload } from "lucide-react";

type FilePickButtonProps = {
  label: string;
  accept: string;
  disabled?: boolean;
  variant?: "contained" | "outlined";
  onPick: (file: File) => void;
};

/** A real button that opens the file picker, so it works with keyboard and screen readers. */
export function FilePickButton({ label, accept, disabled, variant = "outlined", onPick }: FilePickButtonProps) {
  const input = useRef<HTMLInputElement>(null);
  return (
    <>
      <Button
        variant={variant}
        disabled={disabled}
        startIcon={<Upload className="size-4" aria-hidden="true" />}
        onClick={() => input.current?.click()}
        sx={{ minHeight: 44, borderRadius: "14px" }}
      >
        {label}
      </Button>
      <input
        ref={input}
        type="file"
        accept={accept}
        hidden
        tabIndex={-1}
        aria-hidden="true"
        onChange={(event) => {
          const file = event.target.files?.[0];
          event.target.value = "";
          if (file) onPick(file);
        }}
      />
    </>
  );
}
