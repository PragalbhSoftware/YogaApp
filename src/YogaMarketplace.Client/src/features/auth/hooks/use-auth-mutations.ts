import { useMutation, useQueryClient } from "@tanstack/react-query";
import { authApi } from "@/features/auth/api/auth-api";
import { currentUserKey } from "@/features/auth/hooks/use-current-user-sync";

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

export function useAcceptConsent() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: authApi.acceptConsent,
    onMutate: () => queryClient.cancelQueries({ queryKey: currentUserKey }),
    onSuccess: (user) => queryClient.setQueryData([...currentUserKey, user.id], user),
  });
}

export function useVerifyOtp() {
  return useMutation({
    mutationFn: ({ phone, code }: { phone: string; code: string }) =>
      authApi.verifyOtp(phone, code),
  });
}
