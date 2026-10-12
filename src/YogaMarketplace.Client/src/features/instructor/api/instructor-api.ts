import type { AxiosRequestConfig } from "axios";
import { http } from "@/services/http/http-client";
import type { SessionMode } from "@/constants/catalog";
import type { BookingRecord } from "@/features/bookings/types";
import type {
  AddCertificateInput,
  AddSlotInput,
  ProviderCertificate,
  ProviderMedia,
  ProviderPhoto,
  ProviderService,
  ProviderTrial,
  UpdateProviderTrialInput,
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

const UPLOAD_TIMEOUT_MS = 120_000;

/** Lets the browser set the multipart boundary and gives slow mobile uploads time to finish. */
const uploadConfig: AxiosRequestConfig = {
  headers: { "Content-Type": "multipart/form-data" },
  timeout: UPLOAD_TIMEOUT_MS,
};

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

  services() {
    return http.get<ProviderService[]>("/api/providers/me/services").then((res) => res.data);
  },

  trial() {
    return http.get<ProviderTrial>("/api/providers/me/trial").then((res) => res.data);
  },

  updateTrial(input: UpdateProviderTrialInput) {
    return http.put<ProviderTrial>("/api/providers/me/trial", input).then((res) => res.data);
  },

  media() {
    return http.get<ProviderMedia>("/api/providers/me/media").then((res) => res.data);
  },

  addPhoto(file: File) {
    const form = new FormData();
    form.append("file", file);
    return http.post<ProviderPhoto>("/api/providers/me/photos", form, uploadConfig).then((res) => res.data);
  },

  deletePhoto(id: string) {
    return http.delete(`/api/providers/me/photos/${id}`).then(() => undefined);
  },

  setIntroVideo(url: string) {
    return http
      .put<ProviderMedia>("/api/providers/me/intro-video", { url: url || null })
      .then((res) => res.data);
  },

  addCertificate(input: AddCertificateInput) {
    const form = new FormData();
    form.append("title", input.title);
    form.append("file", input.file);
    return http
      .post<ProviderCertificate>("/api/providers/me/certificates", form, uploadConfig)
      .then((res) => res.data);
  },

  deleteCertificate(id: string) {
    return http.delete(`/api/providers/me/certificates/${id}`).then(() => undefined);
  },

  certificateFile(id: string) {
    return http
      .get<Blob>(`/api/providers/me/certificates/${id}/file`, { responseType: "blob" })
      .then((res) => res.data);
  },
};
