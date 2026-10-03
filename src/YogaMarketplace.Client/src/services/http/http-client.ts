import axios, { type AxiosError, type AxiosInstance } from "axios";
import { toast } from "sonner";
import type { VerifyResponse } from "@/features/auth/types";
import { useAuthStore } from "@/stores/auth-store";
import { ApiError } from "@/services/http/api-error";

declare module "axios" {
  interface AxiosRequestConfig {
    /** Auth endpoints that must never trigger a refresh themselves. */
    skipAuthRefresh?: boolean;
  }
  interface InternalAxiosRequestConfig {
    _retriedAfterRefresh?: boolean;
  }
}

type ErrorBody = { error?: string };

const FALLBACK_MESSAGE = "Something went wrong. Try again.";
const SESSION_ENDED = "Your session has ended. Sign in again.";
export const REFRESH_PATH = "/api/auth/refresh";

async function readErrorBody(data: unknown): Promise<ErrorBody | undefined> {
  if (data instanceof Blob) {
    if (!data.type.includes("json")) return undefined;
    try {
      return JSON.parse(await data.text()) as ErrorBody;
    } catch {
      return undefined;
    }
  }
  return data as ErrorBody | undefined;
}

async function readApiError(error: AxiosError<ErrorBody | Blob>) {
  if (!error.response) {
    return new ApiError(
      "We couldn't reach the marketplace. Start the API and try again.",
      0,
      true,
    );
  }

  const body = await readErrorBody(error.response.data);
  return new ApiError(body?.error ?? FALLBACK_MESSAGE, error.response.status);
}

export const http: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "",
  timeout: 15_000,
  headers: { "Content-Type": "application/json" },
  withCredentials: true,
});

type RefreshResult = { ok: true; token: string } | { ok: false; error: unknown };

let refreshInFlight: Promise<RefreshResult> | null = null;

async function requestRefresh(): Promise<RefreshResult> {
  let lastError: unknown;
  // A 409 means another tab rotated the cookie a moment ago; the browser now holds the newer one.
  for (let attempt = 0; attempt < 2; attempt++) {
    try {
      const { data } = await http.post<VerifyResponse>(REFRESH_PATH, undefined, { skipAuthRefresh: true });
      useAuthStore.getState().signIn(data.token, data.user);
      return { ok: true, token: data.token };
    } catch (error) {
      lastError = error;
      if (!(error instanceof ApiError) || error.status !== 409) break;
    }
  }
  return { ok: false, error: lastError };
}

/** One refresh at a time; concurrent 401s wait for the same result. */
export function refreshSession(): Promise<RefreshResult> {
  refreshInFlight ??= requestRefresh().finally(() => {
    refreshInFlight = null;
  });
  return refreshInFlight;
}

function endSession(reason?: unknown) {
  if (!useAuthStore.getState().token) return;
  useAuthStore.getState().signOut();
  const message = reason instanceof ApiError && reason.status === 403 ? reason.message : SESSION_ENDED;
  toast.error(message, { id: "session-ended" });
}

http.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ErrorBody | Blob>) => {
    const apiError = await readApiError(error);
    const config = error.config;
    const canRefresh =
      apiError.status === 401 && config && !config.skipAuthRefresh && useAuthStore.getState().token;
    if (!canRefresh) return Promise.reject(apiError);

    if (config._retriedAfterRefresh) {
      endSession();
      return Promise.reject(apiError);
    }

    const refreshed = await refreshSession();
    if (!refreshed.ok) {
      endSession(refreshed.error);
      return Promise.reject(apiError);
    }

    config._retriedAfterRefresh = true;
    return http.request(config);
  },
);
