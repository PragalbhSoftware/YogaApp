export class ApiError extends Error {
  readonly status: number;
  readonly unreachable: boolean;
  /** The id sent as `X-Correlation-Id`; the server logs the request under it. */
  readonly correlationId?: string;

  constructor(message: string, status = 0, unreachable = false, correlationId?: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.unreachable = unreachable;
    this.correlationId = correlationId;
  }
}

/** The request may succeed if sent again: the network dropped or the server failed. */
export function isRetryable(error: unknown) {
  return error instanceof ApiError && (error.unreachable || error.status === 0 || error.status >= 500);
}

export function toUserMessage(error: unknown) {
  if (error instanceof ApiError) return error.message;
  return "Something went wrong. Try again.";
}
