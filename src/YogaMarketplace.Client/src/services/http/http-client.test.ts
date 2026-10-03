import { AxiosError, AxiosHeaders, type AxiosResponse, type InternalAxiosRequestConfig } from "axios";
import { toast } from "sonner";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/services/http/api-error";
import { http, REFRESH_PATH } from "@/services/http/http-client";
import { useAuthStore } from "@/stores/auth-store";

vi.mock("sonner", () => ({ toast: { error: vi.fn() } }));

type Reply = { status: number; data?: unknown };
type Route = (config: InternalAxiosRequestConfig) => Reply;

const originalAdapter = http.defaults.adapter;
const customer = { id: "u1", name: "Asha", phone: "+919000000000", gender: null, role: "Customer" };

function serve(route: Route) {
  const calls: InternalAxiosRequestConfig[] = [];
  http.defaults.adapter = (config) => {
    calls.push(config);
    const reply = route(config);
    const response: AxiosResponse = {
      status: reply.status,
      statusText: "",
      headers: {},
      config: { ...config, headers: new AxiosHeaders(config.headers) },
      data: reply.data,
    };
    if (reply.status < 400) return Promise.resolve(response);
    return Promise.reject(new AxiosError("Request failed", "ERR_BAD_REQUEST", config, null, response));
  };
  return calls;
}

const bearer = (config: InternalAxiosRequestConfig) => String(config.headers.Authorization ?? "");
const refreshCalls = (calls: InternalAxiosRequestConfig[]) => calls.filter((call) => call.url === REFRESH_PATH);
const refreshed = (token: string): Reply => ({ status: 200, data: { token, user: customer } });

describe("http client session handling", () => {
  beforeEach(() => {
    useAuthStore.getState().signIn("expired-token", customer);
  });

  afterEach(() => {
    http.defaults.adapter = originalAdapter;
    useAuthStore.getState().signOut();
    vi.mocked(toast.error).mockClear();
  });

  it("refreshes silently on 401 and retries the request once with the new token", async () => {
    const calls = serve((config) => {
      if (config.url === REFRESH_PATH) return refreshed("fresh-token");
      return bearer(config) === "Bearer fresh-token" ? { status: 200, data: ["booking"] } : { status: 401 };
    });

    const response = await http.get("/api/bookings/me");

    expect(response.data).toEqual(["booking"]);
    expect(useAuthStore.getState().token).toBe("fresh-token");
    expect(refreshCalls(calls)).toHaveLength(1);
    expect(toast.error).not.toHaveBeenCalled();
  });

  it("shares one refresh between concurrent 401s", async () => {
    const calls = serve((config) => {
      if (config.url === REFRESH_PATH) return refreshed("fresh-token");
      return bearer(config) === "Bearer fresh-token" ? { status: 200, data: config.url } : { status: 401 };
    });

    const results = await Promise.all([http.get("/api/a"), http.get("/api/b"), http.get("/api/c")]);

    expect(results.map((result) => result.data)).toEqual(["/api/a", "/api/b", "/api/c"]);
    expect(refreshCalls(calls)).toHaveLength(1);
  });

  it("retries the refresh once when another tab rotated the cookie first", async () => {
    let refreshAttempts = 0;
    serve((config) => {
      if (config.url === REFRESH_PATH) {
        refreshAttempts++;
        return refreshAttempts === 1 ? { status: 409, data: { error: "Try again." } } : refreshed("fresh-token");
      }
      return bearer(config) === "Bearer fresh-token" ? { status: 200 } : { status: 401 };
    });

    await http.get("/api/bookings/me");

    expect(refreshAttempts).toBe(2);
    expect(useAuthStore.getState().token).toBe("fresh-token");
  });

  it("signs out cleanly when the refresh is rejected", async () => {
    serve((config) =>
      config.url === REFRESH_PATH ? { status: 401, data: { error: "Your session has ended." } } : { status: 401 },
    );

    const error = await http.get("/api/bookings/me").catch((reason: unknown) => reason);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(401);
    expect(useAuthStore.getState().token).toBeNull();
    expect(useAuthStore.getState().user).toBeNull();
    expect(toast.error).toHaveBeenCalledWith("Your session has ended. Sign in again.", { id: "session-ended" });
  });

  it("signs a blocked user out on their next request with the suspension message", async () => {
    const suspended = "This account is suspended. Contact support.";
    const calls = serve((config) =>
      config.url === REFRESH_PATH ? { status: 403, data: { error: suspended } } : { status: 401 },
    );

    await http.get("/api/bookings/me").catch(() => undefined);

    expect(useAuthStore.getState().token).toBeNull();
    expect(refreshCalls(calls)).toHaveLength(1);
    expect(toast.error).toHaveBeenCalledWith(suspended, { id: "session-ended" });
  });

  it("does not loop when the retried request is still unauthorized", async () => {
    const calls = serve((config) => (config.url === REFRESH_PATH ? refreshed("fresh-token") : { status: 401 }));

    await http.get("/api/bookings/me").catch(() => undefined);

    expect(refreshCalls(calls)).toHaveLength(1);
    expect(calls.filter((call) => call.url === "/api/bookings/me")).toHaveLength(2);
    expect(useAuthStore.getState().token).toBeNull();
  });

  it("keeps the session and surfaces the API message on other errors", async () => {
    const calls = serve(() => ({ status: 403, data: { error: "Admin access required." } }));

    const error = await http.get("/api/admin/users").catch((reason: unknown) => reason);

    expect((error as ApiError).message).toBe("Admin access required.");
    expect(useAuthStore.getState().token).toBe("expired-token");
    expect(refreshCalls(calls)).toHaveLength(0);
  });

  it("never refreshes for signed-out visitors", async () => {
    useAuthStore.getState().signOut();
    const calls = serve(() => ({ status: 401, data: { error: "Sign in required." } }));

    await http.get("/api/auth/me").catch(() => undefined);

    expect(refreshCalls(calls)).toHaveLength(0);
  });
});
