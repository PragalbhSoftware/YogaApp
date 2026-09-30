import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Rating } from "@mui/material";
import { Star } from "lucide-react";
import { AddressField } from "@/features/profile/components/address-field";
import {
  emptyReviewForm,
  reviewFormSchema,
  toReviewPayload,
  type ReviewFormInput,
  type ReviewFormValues,
} from "@/features/bookings/schemas";
import type { CreateReviewInput } from "@/features/bookings/types";
import { brand } from "@/constants/brand";

const ratingLabels: Record<number, string> = {
  1: "Poor",
  2: "Fair",
  3: "Good",
  4: "Very good",
  5: "Excellent",
};

type ReviewFormProps = {
  busy: boolean;
  onSubmit: (values: CreateReviewInput) => void;
};

export function ReviewForm({ busy, onSubmit }: ReviewFormProps) {
  const form = useForm<ReviewFormInput, unknown, ReviewFormValues>({
    resolver: zodResolver(reviewFormSchema),
    defaultValues: emptyReviewForm,
  });

  return (
    <form
      className="space-y-5 rounded-2xl bg-brand-background px-4 py-4"
      onSubmit={form.handleSubmit((values) => onSubmit(toReviewPayload(values)))}
      noValidate
    >
      <div>
        <h3 className="font-heading text-lg font-medium">How was this session?</h3>
        <p className="mt-1 text-sm leading-relaxed text-brand-muted">
          Your rating helps other students. You can leave one review.
        </p>
      </div>

      <Controller
        name="rating"
        control={form.control}
        render={({ field, fieldState }) => (
          <div className="flex flex-col gap-2">
            <span id={`${field.name}-label`} className="text-sm font-medium text-brand-text">
              Rating
            </span>
            <Rating
              name="session-rating"
              value={field.value}
              onChange={(_, value) => field.onChange(value)}
              onBlur={field.onBlur}
              disabled={busy}
              getLabelText={(value) => `${value} ${value === 1 ? "star" : "stars"}, ${ratingLabels[value]}`}
              icon={<Star className="size-8" fill="currentColor" strokeWidth={1.5} />}
              emptyIcon={<Star className="size-8" strokeWidth={1.5} />}
              sx={{
                color: brand.accent,
                gap: "4px",
                "& .MuiRating-iconFilled": { color: brand.accent },
                "& .MuiRating-iconHover": { color: brand.accent },
                "& .MuiRating-iconEmpty": { color: brand.textSecondary, opacity: 0.45 },
              }}
              slotProps={{
                root: { "aria-labelledby": `${field.name}-label` },
              }}
            />
            <p className="min-h-5 text-sm text-brand-muted">
              {typeof field.value === "number" && field.value >= 1
                ? ratingLabels[field.value]
                : "Tap a star to rate."}
            </p>
            {fieldState.error ? (
              <p className="text-sm text-red-700" role="alert">
                {fieldState.error.message}
              </p>
            ) : null}
          </div>
        )}
      />

      <Controller
        name="comment"
        control={form.control}
        render={({ field, fieldState }) => (
          <AddressField
            id="review-comment"
            label="Comment (optional)"
            placeholder="What helped, or what could be better"
            multiline
            minRows={3}
            maxLength={1000}
            value={field.value}
            onChange={field.onChange}
            onBlur={field.onBlur}
            inputRef={field.ref}
            error={fieldState.error?.message}
          />
        )}
      />

      <Button
        type="submit"
        variant="contained"
        fullWidth
        disabled={busy || form.formState.isSubmitting}
        sx={{ minHeight: 44, borderRadius: "14px" }}
      >
        {busy ? "Saving…" : "Submit review"}
      </Button>
    </form>
  );
}
