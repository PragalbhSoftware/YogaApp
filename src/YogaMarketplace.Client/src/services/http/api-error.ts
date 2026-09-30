export class ApiError extends Error {
  readonly status: number;
  readonly unreachable: boolean;

  constructor(message: string, status = 0, unreachable = false) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.unreachable = unreachable;
  }
}

export function toUserMessage(error: unknown) {
  if (error instanceof ApiError) return error.message;
  return "Something went wrong. Try again.";
}
