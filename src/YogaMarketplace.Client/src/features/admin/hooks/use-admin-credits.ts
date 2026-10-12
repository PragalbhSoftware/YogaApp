import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { adminCreditsApi } from "@/features/admin/api/admin-credits-api";
import type { CreatePackInput, UpdatePackInput } from "@/features/admin/types";

function invalidatePacks(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ["admin-packs"] });
  void queryClient.invalidateQueries({ queryKey: ["packs"] });
}

export function useAdminPacks() {
  return useQuery({ queryKey: ["admin-packs"], queryFn: adminCreditsApi.packs });
}

export function useCreatePack() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreatePackInput) => adminCreditsApi.createPack(input),
    onSuccess: () => invalidatePacks(queryClient),
  });
}

export function useUpdatePack() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string } & UpdatePackInput) => {
      const { id, ...changes } = input;
      return adminCreditsApi.updatePack(id, changes);
    },
    onSettled: () => invalidatePacks(queryClient),
  });
}

export function useUserCredits(userId: string | undefined) {
  return useQuery({
    queryKey: ["admin-user-credits", userId],
    queryFn: () => adminCreditsApi.userCredits(userId ?? ""),
    enabled: Boolean(userId),
  });
}

export function useRefundCreditBatch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (batchId: string) => adminCreditsApi.refundBatch(batchId),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ["admin-user-credits"] });
      void queryClient.invalidateQueries({ queryKey: ["admin-payments"] });
    },
  });
}
