import type { AxiosResponse } from "axios";
import { http } from "@/services/http/http-client";
import {
  errorsPageSize,
  jobsPageSize,
  reconciliationPageSize,
  type ActiveCity,
  type ActiveState,
  type AdminArea,
  type AdminCity,
  type AdminState,
  type AdminErrorEvent,
  type AdminCertificate,
  type AdminBooking,
  type AdminCategory,
  type AdminJob,
  type AdminPayment,
  type AdminPayout,
  type AdminProvider,
  type AdminReport,
  type AdminSettings,
  type AdminUserDetail,
  type AdminUserSummary,
  type BackgroundJobStatus,
  type BookingListQuery,
  type PagedList,
  type ReconciliationItem,
  type ReconciliationItemQuery,
  type ReconciliationRun,
  type SettingsAuditEntry,
  type UpdateSettingsInput,
  type UserListQuery,
} from "@/features/admin/types";

export type CsvFile = { blob: Blob; fileName: string };

const FALLBACK_CSV_NAME = "payouts.csv";

function toCsvFile(res: AxiosResponse<Blob>): CsvFile {
  const disposition = String(res.headers["content-disposition"] ?? "");
  const match = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(disposition);
  const fileName = match ? decodeURIComponent(match[1] ?? match[2] ?? FALLBACK_CSV_NAME) : FALLBACK_CSV_NAME;
  return { blob: res.data, fileName };
}

export const adminApi = {
  summary() {
    return http.get<AdminReport>("/api/admin/reports/summary").then((res) => res.data);
  },

  providers(status?: string) {
    return http
      .get<AdminProvider[]>("/api/admin/providers", { params: status ? { status } : undefined })
      .then((res) => res.data);
  },

  verifyProvider(id: string) {
    return http.post<AdminProvider>(`/api/admin/providers/${id}/verify`, {}).then((res) => res.data);
  },

  rejectProvider(id: string, reason?: string) {
    return http
      .post<AdminProvider>(`/api/admin/providers/${id}/reject`, { reason: reason || null })
      .then((res) => res.data);
  },

  certificates(status: string) {
    return http
      .get<AdminCertificate[]>("/api/admin/certificates", { params: { status } })
      .then((res) => res.data);
  },

  certificateFile(id: string) {
    return http
      .get<Blob>(`/api/admin/certificates/${id}/file`, { responseType: "blob" })
      .then((res) => res.data);
  },

  approveCertificate(id: string) {
    return http.post<AdminCertificate>(`/api/admin/certificates/${id}/approve`, {}).then((res) => res.data);
  },

  rejectCertificate(id: string, reason: string) {
    return http
      .post<AdminCertificate>(`/api/admin/certificates/${id}/reject`, { reason })
      .then((res) => res.data);
  },

  users(query: UserListQuery) {
    return http
      .get<AdminUserSummary[]>("/api/admin/users", {
        params: {
          ...(query.q ? { q: query.q } : {}),
          ...(query.role ? { role: query.role } : {}),
        },
      })
      .then((res) => res.data);
  },

  user(id: string) {
    return http.get<AdminUserDetail>(`/api/admin/users/${id}`).then((res) => res.data);
  },

  blockUser(id: string, reason: string) {
    return http.post<AdminUserDetail>(`/api/admin/users/${id}/block`, { reason }).then((res) => res.data);
  },

  unblockUser(id: string, reason?: string) {
    return http
      .post<AdminUserDetail>(`/api/admin/users/${id}/unblock`, { reason: reason || null })
      .then((res) => res.data);
  },

  bookings(query: BookingListQuery) {
    return http
      .get<AdminBooking[]>("/api/admin/bookings", {
        params: {
          ...(query.status ? { status: query.status } : {}),
          ...(query.from ? { from: query.from } : {}),
          ...(query.to ? { to: query.to } : {}),
        },
      })
      .then((res) => res.data);
  },

  booking(id: string) {
    return http.get<AdminBooking>(`/api/admin/bookings/${id}`).then((res) => res.data);
  },

  cancelBooking(id: string, reason: string) {
    return http.post<AdminBooking>(`/api/admin/bookings/${id}/cancel`, { reason }).then((res) => res.data);
  },

  payments(status?: string) {
    return http
      .get<AdminPayment[]>("/api/admin/payments", { params: status ? { status } : undefined })
      .then((res) => res.data);
  },

  payouts(status?: string) {
    return http
      .get<AdminPayout[]>("/api/admin/payouts", { params: status ? { status } : undefined })
      .then((res) => res.data);
  },

  exportPendingPayouts() {
    return http.post<Blob>("/api/admin/payouts/export", {}, { responseType: "blob" }).then(toCsvFile);
  },

  downloadPayoutsCsv(status: string) {
    return http
      .get<Blob>("/api/admin/payouts/csv", { params: { status }, responseType: "blob" })
      .then(toCsvFile);
  },

  markPayoutPaid(id: string) {
    return http.post<AdminPayout>(`/api/admin/payouts/${id}/paid`, {}).then((res) => res.data);
  },

  reconciliationRuns(page = 1) {
    return http
      .get<PagedList<ReconciliationRun>>("/api/admin/reconciliation/runs", {
        params: { page, pageSize: reconciliationPageSize },
      })
      .then((res) => res.data);
  },

  reconciliationItems(query: ReconciliationItemQuery) {
    return http
      .get<PagedList<ReconciliationItem>>("/api/admin/reconciliation/items", {
        params: {
          ...(query.runId ? { runId: query.runId } : {}),
          ...(query.type ? { type: query.type } : {}),
          ...(query.status ? { status: query.status } : {}),
          page: query.page ?? 1,
          pageSize: reconciliationPageSize,
        },
      })
      .then((res) => res.data);
  },

  resolveReconciliationItem(id: string, note: string) {
    return http
      .post<ReconciliationItem>(`/api/admin/reconciliation/items/${id}/resolve`, { note })
      .then((res) => res.data);
  },

  jobs(status: BackgroundJobStatus, page = 1) {
    return http
      .get<PagedList<AdminJob>>("/api/admin/jobs", { params: { status, page, pageSize: jobsPageSize } })
      .then((res) => res.data);
  },

  retryJob(id: string) {
    return http.post<AdminJob>(`/api/admin/jobs/${id}/retry`, {}).then((res) => res.data);
  },

  errors(acknowledged: boolean, page = 1) {
    return http
      .get<PagedList<AdminErrorEvent>>("/api/admin/errors", { params: { acknowledged, page, pageSize: errorsPageSize } })
      .then((res) => res.data);
  },

  errorSummary() {
    return http.get<{ unacknowledged: number }>("/api/admin/errors/summary").then((res) => res.data);
  },

  acknowledgeError(id: string) {
    return http.post<AdminErrorEvent>(`/api/admin/errors/${id}/acknowledge`, {}).then((res) => res.data);
  },

  sendTestAlert() {
    return http.post<void>("/api/admin/errors/test-alert", {}).then(() => undefined);
  },

  areas() {
    return http.get<AdminArea[]>("/api/admin/areas").then((res) => res.data);
  },

  createArea(input: { name: string; city: string; stateId: string }) {
    return http.post<AdminArea>("/api/admin/areas", input).then((res) => res.data);
  },

  states(page = 1) {
    return http.get<PagedList<AdminState>>("/api/admin/states", { params: { page } }).then((res) => res.data);
  },

  createState(input: { name: string; code: string; displayOrder: number }) {
    return http.post<AdminState>("/api/admin/states", input).then((res) => res.data);
  },

  importIndiaLocations() {
    return http
      .post<{ statesAdded: number; statesSkipped: number; citiesAdded: number; citiesSkipped: number }>(
        "/api/admin/states/import-india",
      )
      .then((res) => res.data);
  },

  updateState(id: string, input: { name?: string; code?: string; displayOrder?: number; isActive?: boolean }) {
    return http.patch<AdminState>(`/api/admin/states/${id}`, input).then((res) => res.data);
  },

  cities(stateId: string, page = 1) {
    return http
      .get<PagedList<AdminCity>>("/api/admin/cities", { params: { stateId, page } })
      .then((res) => res.data);
  },

  createCity(input: { name: string; stateId: string; displayOrder: number }) {
    return http.post<AdminCity>("/api/admin/cities", input).then((res) => res.data);
  },

  updateCity(id: string, input: { name?: string; stateId?: string; displayOrder?: number; isActive?: boolean }) {
    return http.patch<AdminCity>(`/api/admin/cities/${id}`, input).then((res) => res.data);
  },

  activeStates() {
    return http.get<ActiveState[]>("/api/catalog/states").then((res) => res.data);
  },

  activeCities(stateId: string) {
    return http.get<ActiveCity[]>("/api/catalog/cities", { params: { stateId } }).then((res) => res.data);
  },

  updateArea(id: string, input: { name?: string; isActive?: boolean }) {
    return http.patch<AdminArea>(`/api/admin/areas/${id}`, input).then((res) => res.data);
  },

  categories() {
    return http.get<AdminCategory[]>("/api/admin/categories").then((res) => res.data);
  },

  renameCategory(id: string, name: string) {
    return http.patch<AdminCategory>(`/api/admin/categories/${id}`, { name }).then((res) => res.data);
  },

  settings() {
    return http.get<AdminSettings>("/api/admin/settings").then((res) => res.data);
  },

  updateSettings(input: UpdateSettingsInput) {
    return http.patch<AdminSettings>("/api/admin/settings", input).then((res) => res.data);
  },

  settingsAudit() {
    return http.get<SettingsAuditEntry[]>("/api/admin/settings/audit").then((res) => res.data);
  },
};
