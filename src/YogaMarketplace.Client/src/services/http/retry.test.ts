import { describe, expect, it, vi } from "vitest";
import { ApiError } from "@/services/http/api-error";
import { withRetry } from "@/services/http/retry";

describe("withRetry", () => {
  it("retries network and server failures, then returns the result", async () => {
    const request = vi
      .fn<() => Promise<string>>()
      .mockRejectedValueOnce(new ApiError("offline", 0, true))
      .mockRejectedValueOnce(new ApiError("down", 503))
      .mockResolvedValue("ok");

    await expect(withRetry(request, [0, 0])).resolves.toBe("ok");
    expect(request).toHaveBeenCalledTimes(3);
  });

  it("does not retry a client error", async () => {
    const request = vi.fn<() => Promise<string>>().mockRejectedValue(new ApiError("taken", 409));

    await expect(withRetry(request, [0, 0])).rejects.toThrow("taken");
    expect(request).toHaveBeenCalledTimes(1);
  });

  it("gives up after the last delay", async () => {
    const request = vi.fn<() => Promise<string>>().mockRejectedValue(new ApiError("down", 503));

    await expect(withRetry(request, [0])).rejects.toThrow("down");
    expect(request).toHaveBeenCalledTimes(2);
  });
});
