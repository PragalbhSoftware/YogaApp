import { http } from "@/services/http/http-client";
import type {
  BookingQuote,
  BookingRecord,
  CancelQuote,
  CheckoutOrder,
  CreateOrderInput,
  CreateReviewInput,
  ReviewRecord,
  TrialStatus,
} from "@/features/bookings/types";

export const bookingsApi = {
  quote(slotId: string, trial: boolean) {
    return http
      .get<BookingQuote>("/api/bookings/quote", { params: trial ? { slotId, trial } : { slotId } })
      .then((res) => res.data);
  },

  trialStatus(providerId: string) {
    return http.get<TrialStatus>("/api/bookings/trial", { params: { providerId } }).then((res) => res.data);
  },

  createOrder(input: CreateOrderInput) {
    return http.post<CheckoutOrder>("/api/bookings/orders", input).then((res) => res.data);
  },

  bookWithCredit(input: CreateOrderInput) {
    return http.post<BookingRecord>("/api/bookings/credit", input).then((res) => res.data);
  },

  confirmLocal(orderId: string) {
    return http.post<BookingRecord>("/api/bookings/local-confirm", { orderId }).then((res) => res.data);
  },

  confirm(orderId: string, paymentId: string, signature: string) {
    return http
      .post<BookingRecord>("/api/bookings/confirm", { orderId, paymentId, signature })
      .then((res) => res.data);
  },

  listMine() {
    return http.get<BookingRecord[]>("/api/bookings/me").then((res) => res.data);
  },

  cancelQuote(id: string) {
    return http.get<CancelQuote>(`/api/bookings/${id}/cancel-quote`).then((res) => res.data);
  },

  cancel(id: string) {
    return http.post<BookingRecord>(`/api/bookings/${id}/cancel`, {}).then((res) => res.data);
  },

  reschedule(id: string, slotId: string) {
    return http
      .post<BookingRecord>(`/api/bookings/${id}/reschedule`, { slotId })
      .then((res) => res.data);
  },

  review(id: string, input: CreateReviewInput) {
    return http.post<ReviewRecord>(`/api/bookings/${id}/reviews`, input).then((res) => res.data);
  },
};
