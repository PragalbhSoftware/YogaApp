import { useRef, useState, type ReactNode } from "react";

type FileDropZoneProps = {
  accept: string;
  label: string;
  disabled?: boolean;
  className?: string;
  onPick: (file: File) => void;
  children: ReactNode;
};

/** A dashed tile that opens the file picker on click or Enter, and also accepts a dropped file. */
export function FileDropZone({ accept, label, disabled, className = "", onPick, children }: FileDropZoneProps) {
  const input = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);

  return (
    <>
      <button
        type="button"
        aria-label={label}
        disabled={disabled}
        onClick={() => input.current?.click()}
        onDragOver={(event) => {
          if (disabled) return;
          event.preventDefault();
          setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(event) => {
          event.preventDefault();
          setDragging(false);
          const file = event.dataTransfer.files?.[0];
          if (file && !disabled) onPick(file);
        }}
        className={`flex flex-col items-center justify-center gap-1 rounded-2xl border-2 border-dashed text-center transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary disabled:cursor-not-allowed disabled:opacity-50 ${
          dragging
            ? "border-brand-primary bg-brand-primary/10"
            : "border-brand-border bg-brand-background hover:border-brand-primary/60"
        } ${className}`}
      >
        {children}
      </button>
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
