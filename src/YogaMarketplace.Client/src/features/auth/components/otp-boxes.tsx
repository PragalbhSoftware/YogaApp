import { useRef, type KeyboardEvent } from "react";

type OtpBoxesProps = {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
};

export function OtpBoxes({ value, onChange, disabled }: OtpBoxesProps) {
  const chars = value.replace(/\D/g, "").slice(0, 6);
  const refs = useRef<Array<HTMLInputElement | null>>([]);

  function updateAt(index: number, digit: string) {
    const next = chars.split("");
    while (next.length < 6) next.push("");
    next[index] = digit;
    onChange(next.join("").slice(0, 6));
  }

  function handleChange(index: number, raw: string) {
    const incoming = raw.replace(/\D/g, "");
    if (incoming.length > 1) {
      onChange(incoming.slice(0, 6));
      refs.current[Math.min(incoming.length, 5)]?.focus();
      return;
    }
    if (!incoming) {
      updateAt(index, "");
      return;
    }
    updateAt(index, incoming);
    refs.current[index + 1]?.focus();
  }

  function handleKeyDown(index: number, event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Backspace" && !chars[index] && index > 0) {
      updateAt(index - 1, "");
      refs.current[index - 1]?.focus();
    }
  }

  return (
    <div className="flex justify-between gap-2" role="group" aria-label="Six-digit verification code">
      {Array.from({ length: 6 }, (_, index) => (
        <input
          key={index}
          ref={(node) => {
            refs.current[index] = node;
          }}
          type="text"
          inputMode="numeric"
          autoComplete={index === 0 ? "one-time-code" : "off"}
          maxLength={1}
          disabled={disabled}
          aria-label={`Digit ${index + 1}`}
          value={chars[index] ?? ""}
          onChange={(event) => handleChange(index, event.target.value)}
          onKeyDown={(event) => handleKeyDown(index, event)}
          onPaste={(event) => {
            event.preventDefault();
            const pasted = event.clipboardData.getData("text").replace(/\D/g, "").slice(0, 6);
            if (!pasted) return;
            onChange(pasted);
            refs.current[Math.min(pasted.length, 5)]?.focus();
          }}
          className="h-14 w-full max-w-14 rounded-[14px] border border-brand-border bg-brand-surface text-center text-xl font-medium text-brand-text outline-none transition-colors focus:border-brand-primary focus:ring-2 focus:ring-brand-primary/20"
        />
      ))}
    </div>
  );
}
