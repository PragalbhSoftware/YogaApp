import { http } from "@/services/http/http-client";
import type { OtpResponse, User, VerifyResponse } from "@/features/auth/types";

export const authApi = {
  requestOtp(input: {
    phone: string;
    name?: string;
    gender?: string;
    isNewUser: boolean;
  }) {
    return http.post<OtpResponse>("/api/auth/otp/request", input).then((res) => res.data);
  },

  resendOtp(phone: string) {
    return http.post<OtpResponse>("/api/auth/otp/resend", { phone }).then((res) => res.data);
  },

  verifyOtp(phone: string, code: string) {
    return http
      .post<VerifyResponse>("/api/auth/otp/verify", { phone, code })
      .then((res) => res.data);
  },

  me() {
    return http.get<User>("/api/auth/me").then((res) => res.data);
  },

  logout() {
    return http.post<void>("/api/auth/logout", undefined, { skipAuthRefresh: true }).then(() => undefined);
  },
};
