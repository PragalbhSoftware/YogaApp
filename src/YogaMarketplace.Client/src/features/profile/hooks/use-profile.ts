import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { profileApi } from "@/features/profile/api/profile-api";
import type { UpdateCustomerAccountInput, VisitAddressInput } from "@/features/profile/types";
import { useAuthStore } from "@/stores/auth-store";

export const profileQueryKey = ["profile"] as const;

export function useCustomerProfile() {
  return useQuery({
    queryKey: profileQueryKey,
    queryFn: profileApi.me,
  });
}

export function useUpdateCustomerAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateCustomerAccountInput) => profileApi.updateAccount(input),
    onSuccess: (profile) => {
      queryClient.setQueryData(profileQueryKey, profile);
      const current = useAuthStore.getState().user;
      const token = useAuthStore.getState().token;
      if (current && token) {
        useAuthStore.getState().signIn(token, {
          ...current,
          name: profile.name,
          gender: profile.gender,
        });
      }
    },
  });
}

export function useSaveVisitAddress() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: VisitAddressInput) => profileApi.saveVisitAddress(input),
    onSuccess: (profile) => {
      queryClient.setQueryData(profileQueryKey, profile);
      void queryClient.invalidateQueries({ queryKey: profileQueryKey });
    },
  });
}
