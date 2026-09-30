import axios, { type AxiosError, type AxiosInstance } from "axios";
import { useAuthStore } from "@/stores/auth-store";
import { ApiError } from "@/services/http/api-error";

type ErrorBody = { error?: string };

const FALLBACK_MESSAGE = "Something went wrong. Try again.";

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
});

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
    if (apiError.status === 401 && useAuthStore.getState().token) {
      useAuthStore.getState().signOut();
    }
    return Promise.reject(apiError);
  },
);
