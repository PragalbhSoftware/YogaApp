import { http } from "@/services/http/http-client";
import type { SessionMode } from "@/constants/catalog";
import type { BookingRecord } from "@/features/bookings/types";
import type {
  AddSlotInput,
  InstructorProfile,
  OwnedSlot,
  OwnedSlotList,
  RegisterInstructorInput,
  RegisterInstructorResult,
  InstructorPayout,
  UpdateInstructorRatesInput,
  UpdateInstructorProfileInput,
  UpdateSlotInput,
} from "@/features/instructor/types";

export const instructorApi = {
  me() {
    return http.get<InstructorProfile>("/api/providers/me").then((res) => res.data);
  },

  updateProfile(input: UpdateInstructorProfileInput) {
    return http.patch<InstructorProfile>("/api/providers/me", input).then((res) => res.data);
  },

  payouts(status?: string) {
    return http
      .get<InstructorPayout[]>("/api/providers/me/payouts", {
        params: status ? { status } : undefined,
      })
      .then((res) => res.data);
  },

  updateRates(input: UpdateInstructorRatesInput) {
    return http.patch<InstructorProfile>("/api/providers/me/rates", input).then((res) => res.data);
  },

  register(input: RegisterInstructorInput) {
    return http.post<RegisterInstructorResult>("/api/providers/register", input).then((res) => res.data);
  },

  slots(mode: SessionMode, from: string, to: string) {
    return http
      .get<OwnedSlotList>("/api/providers/me/slots", { params: { mode, from, to } })
      .then((res) => res.data);
  },

  addSlots(input: AddSlotInput) {
    return http
      .post<OwnedSlot[]>("/api/providers/me/slots", {
        mode: input.mode,
        slots: [{ date: input.date, start: input.start, end: input.end }],
      })
      .then((res) => res.data);
  },

  updateSlot(input: UpdateSlotInput) {
    return http
      .put<OwnedSlot>(`/api/providers/me/slots/${input.id}`, {
        date: input.date,
        start: input.start,
        end: input.end,
      })
      .then((res) => res.data);
  },

  blockSlot(id: string) {
    return http.post<OwnedSlot>(`/api/providers/me/slots/${id}/block`, {}).then((res) => res.data);
  },

  unblockSlot(id: string) {
    return http.post<OwnedSlot>(`/api/providers/me/slots/${id}/unblock`, {}).then((res) => res.data);
  },

  deleteSlot(id: string) {
    return http.delete(`/api/providers/me/slots/${id}`).then(() => undefined);
  },

  bookings(status?: string) {
    return http
      .get<BookingRecord[]>("/api/bookings/instructor", { params: status ? { status } : undefined })
      .then((res) => res.data);
  },

  accept(id: string) {
    return http.post<BookingRecord>(`/api/bookings/${id}/accept`, {}).then((res) => res.data);
  },

  decline(id: string) {
    return http.post<BookingRecord>(`/api/bookings/${id}/decline`, {}).then((res) => res.data);
  },

  complete(id: string) {
    return http.post<BookingRecord>(`/api/bookings/${id}/complete`, {}).then((res) => res.data);
  },

  markNoShow(id: string) {
    return http.post<BookingRecord>(`/api/bookings/${id}/noshow`, {}).then((res) => res.data);
  },
};
