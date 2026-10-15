import { toast, type ExternalToast } from "sonner";
import { ApiError, toUserMessage } from "@/services/http/api-error";

/** Error toast with a "Ref: <id>" line users can quote to support, when the error came from an API call. */
export function toastError(error: unknown, message = toUserMessage(error), options?: ExternalToast) {
  const ref = error instanceof ApiError ? error.correlationId : undefined;
  const merged = ref ? { ...options, description: `Ref: ${ref}` } : options;
  if (merged) toast.error(message, merged);
  else toast.error(message);
}
