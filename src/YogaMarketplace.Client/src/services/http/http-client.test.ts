import { AxiosError, AxiosHeaders, type AxiosAdapter, type InternalAxiosRequestConfig } from "axios";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { ApiError } from "@/services/http/api-error";
import { http } from "@/services/http/http-client";
import { useAuthStore } from "@/stores/auth-store";

const originalAdapter = http.defaults.adapter;

function respondWith(status: number, data: unknown): AxiosAdapter {
  return (config: InternalAxiosRequestConfig) =>
    Promise.reject(
      new AxiosError("Request failed", "ERR_BAD_REQUEST", config, null, {
        status,
        statusText: "",
        headers: {},
        config: { ...config, headers: new AxiosHeaders() },
        data,
      }),
    );
}

describe("http client", () => {
  beforeEach(() => {
    useAuthStore.getState().signIn("stale-token", { id: "u1", name: null, phone: "+919000000000", gender: null, role: "Customer" });
  });

  afterEach(() => {
    http.defaults.adapter = originalAdapter;
    useAuthStore.getState().signOut();
  });

  it("signs out when the API rejects the token", async () => {
    http.defaults.adapter = respondWith(401, { error: "Sign in required." });

    const error = await http.get("/api/auth/me").catch((reason: unknown) => reason);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(401);
    expect(useAuthStore.getState().token).toBeNull();
  });

  it("keeps the session and surfaces the API message on other errors", async () => {
    http.defaults.adapter = respondWith(403, { error: "This account is suspended. Contact support." });

    const error = await http.post("/api/auth/otp/request", {}).catch((reason: unknown) => reason);

    expect((error as ApiError).message).toBe("This account is suspended. Contact support.");
    expect(useAuthStore.getState().token).toBe("stale-token");
  });
});
