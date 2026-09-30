import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button, MenuItem, TextField } from "@mui/material";
import { kolkataToday, timeSelectOptions, toHHmm } from "@/utils/clock";

const schema = z
  .object({
    date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, "Choose a date."),
    start: z.string().min(1, "Start time is required."),
    end: z.string().min(1, "End time is required."),
  })
  .superRefine((value, ctx) => {
    const today = kolkataToday();
    if (value.date < today) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["date"], message: "Slots cannot be in the past." });
    }
    const start = toHHmm(value.start);
    const end = toHHmm(value.end);
    if (end <= start) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["end"],
        message: "End time must be after the start time.",
      });
    }
  });

type FormValues = z.infer<typeof schema>;

export type SlotFormValues = { date: string; start: string; end: string };

type SlotFormProps = {
  title: string;
  description: string;
  submitLabel: string;
  busy: boolean;
  defaultValues: SlotFormValues;
  onSubmit: (values: SlotFormValues) => void;
  onCancel?: () => void;
};

const fieldSx = {
  "& .MuiOutlinedInput-root": { borderRadius: "14px" },
};

export function SlotForm({
  title,
  description,
  submitLabel,
  busy,
  defaultValues,
  onSubmit,
  onCancel,
}: SlotFormProps) {
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      date: defaultValues.date,
      start: toHHmm(defaultValues.start),
      end: toHHmm(defaultValues.end),
    },
  });
  const options = timeSelectOptions([defaultValues.start, defaultValues.end]);

  return (
    <form
      className="space-y-4"
      onSubmit={form.handleSubmit((values) =>
        onSubmit({ date: values.date, start: toHHmm(values.start), end: toHHmm(values.end) }),
      )}
      noValidate
    >
      <div>
        <h2 className="font-heading text-lg font-medium">{title}</h2>
        <p className="mt-1 text-sm text-brand-muted">{description}</p>
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        <Controller
          name="date"
          control={form.control}
          render={({ field, fieldState }) => (
            <TextField
              {...field}
              type="date"
              label="Date"
              error={Boolean(fieldState.error)}
              helperText={fieldState.error?.message}
              slotProps={{ htmlInput: { min: kolkataToday() }, inputLabel: { shrink: true } }}
              sx={fieldSx}
            />
          )}
        />
        <Controller
          name="start"
          control={form.control}
          render={({ field, fieldState }) => (
            <TextField
              {...field}
              select
              label="Start"
              error={Boolean(fieldState.error)}
              helperText={fieldState.error?.message ?? "12-hour India time"}
              slotProps={{ inputLabel: { shrink: true } }}
              sx={fieldSx}
            >
              {options.map((option) => (
                <MenuItem key={option.value} value={option.value}>
                  {option.label}
                </MenuItem>
              ))}
            </TextField>
          )}
        />
        <Controller
          name="end"
          control={form.control}
          render={({ field, fieldState }) => (
            <TextField
              {...field}
              select
              label="End"
              error={Boolean(fieldState.error)}
              helperText={fieldState.error?.message}
              slotProps={{ inputLabel: { shrink: true } }}
              sx={fieldSx}
            >
              {options.map((option) => (
                <MenuItem key={option.value} value={option.value}>
                  {option.label}
                </MenuItem>
              ))}
            </TextField>
          )}
        />
      </div>
      <div className="flex flex-col gap-2 sm:flex-row sm:justify-end">
        {onCancel ? (
          <Button type="button" variant="outlined" disabled={busy} onClick={onCancel} sx={{ minHeight: 48, borderRadius: "14px" }}>
            Cancel
          </Button>
        ) : null}
        <Button type="submit" variant="contained" disabled={busy} sx={{ minHeight: 48, borderRadius: "14px" }}>
          {busy ? "Saving…" : submitLabel}
        </Button>
      </div>
    </form>
  );
}
