import type { FieldValues, UseFormReturn } from "react-hook-form";
import { toast } from "sonner";
import type { AdminSettings } from "@/features/admin/types";
import { settingsSaveError } from "@/features/admin/utils";
import { ApiError } from "@/services/http/api-error";
import { toastError } from "@/services/http/toast-error";

/** Settings cards share one settings query, so a save in another card must not wipe this card's unsaved edits. */
export const keepUnsavedEdits = { keepDirtyValues: true, keepDirty: true } as const;

const discardEdits = { keepDirtyValues: false, keepDirty: false } as const;

/**
 * Save callbacks for one settings card: a save clears the card to the saved values, and a 409 discards its edits so the
 * refetched values show (as settingsConflictMessage says).
 */
export function settingsSaveHandlers<T extends FieldValues>(
  form: UseFormReturn<T>,
  toFormValues: (settings: AdminSettings) => T,
  message: string,
) {
  return {
    onSuccess: (saved: AdminSettings) => {
      form.reset(toFormValues(saved), discardEdits);
      toast.success(message);
    },
    onError: (error: unknown) => {
      if (error instanceof ApiError && error.status === 409) form.reset(undefined, discardEdits);
      toastError(error, settingsSaveError(error));
    },
  };
}
