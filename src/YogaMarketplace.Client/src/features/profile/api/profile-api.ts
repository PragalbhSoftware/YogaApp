import { http } from "@/services/http/http-client";
import type { CustomerProfile, UpdateCustomerAccountInput, VisitAddressInput } from "@/features/profile/types";

export const profileApi = {
  me() {
    return http.get<CustomerProfile>("/api/profile").then((res) => res.data);
  },

  updateAccount(input: UpdateCustomerAccountInput) {
    return http.patch<CustomerProfile>("/api/profile", input).then((res) => res.data);
  },

  saveVisitAddress(input: VisitAddressInput) {
    return http.put<CustomerProfile>("/api/profile/visit-address", input).then((res) => res.data);
  },
};
