import { useMutation } from "@tanstack/react-query";
import { authApi } from "@/features/auth/api/auth-api";

export function useRequestOtp() {
  return useMutation({
    mutationFn: authApi.requestOtp,
  });
}

export function useResendOtp() {
  return useMutation({
    mutationFn: authApi.resendOtp,
  });
}

export function useVerifyOtp() {
  return useMutation({
    mutationFn: ({ phone, code }: { phone: string; code: string }) =>
      authApi.verifyOtp(phone, code),
  });
}
