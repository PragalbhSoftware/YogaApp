import { http } from "@/services/http/http-client";
import type { AdminCreditBatch, AdminPack, CreatePackInput, UpdatePackInput } from "@/features/admin/types";

export const adminCreditsApi = {
  packs() {
    return http.get<AdminPack[]>("/api/admin/packs").then((res) => res.data);
  },

  createPack(input: CreatePackInput) {
    return http.post<AdminPack>("/api/admin/packs", input).then((res) => res.data);
  },

  updatePack(id: string, input: UpdatePackInput) {
    return http.patch<AdminPack>(`/api/admin/packs/${id}`, input).then((res) => res.data);
  },

  userCredits(userId: string) {
    return http.get<AdminCreditBatch[]>(`/api/admin/users/${userId}/credits`).then((res) => res.data);
  },

  refundBatch(batchId: string) {
    return http.post<AdminCreditBatch>(`/api/admin/credit-batches/${batchId}/refund`, {}).then((res) => res.data);
  },
};
