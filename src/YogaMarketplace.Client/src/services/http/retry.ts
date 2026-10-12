import { isRetryable } from "@/services/http/api-error";

/** Runs `request`, sending it again after each delay while it fails in a retryable way. Only for idempotent calls. */
export async function withRetry<T>(request: () => Promise<T>, delaysMs: readonly number[]): Promise<T> {
  for (let attempt = 0; ; attempt++) {
    try {
      return await request();
    } catch (error) {
      if (attempt >= delaysMs.length || !isRetryable(error)) throw error;
      await new Promise((resolve) => setTimeout(resolve, delaysMs[attempt]));
    }
  }
}
