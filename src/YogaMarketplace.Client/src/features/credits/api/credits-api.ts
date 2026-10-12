import { http } from "@/services/http/http-client";
import type { MyCredits, Pack, PackOrder, PackOrderStatus } from "@/features/credits/types";

export const creditsApi = {
  listPacks() {
    return http.get<Pack[]>("/api/packs").then((res) => res.data);
  },

  mine() {
    return http.get<MyCredits>("/api/credits/me").then((res) => res.data);
  },

  createOrder(packId: string) {
    return http.post<PackOrder>(`/api/packs/${packId}/orders`, {}).then((res) => res.data);
  },

  confirm(orderId: string, paymentId: string, signature: string) {
    return http
      .post<PackOrderStatus>("/api/packs/confirm", { orderId, paymentId, signature })
      .then((res) => res.data);
  },

  confirmLocal(orderId: string) {
    return http.post<PackOrderStatus>("/api/packs/local-confirm", { orderId }).then((res) => res.data);
  },

  orderStatus(orderId: string) {
    return http.get<PackOrderStatus>(`/api/packs/orders/${encodeURIComponent(orderId)}`).then((res) => res.data);
  },
};
