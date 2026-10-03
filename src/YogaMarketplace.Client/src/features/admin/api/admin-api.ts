import type { AxiosResponse } from "axios";
import { http } from "@/services/http/http-client";
import type {
  AdminArea,
  AdminBooking,
  AdminCategory,
  AdminPayment,
  AdminPayout,
  AdminProvider,
  AdminReport,
  AdminSettings,
  AdminUserDetail,
  AdminUserSummary,
  BookingListQuery,
  SettingsAuditEntry,
  UpdateSettingsInput,
  UserListQuery,
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

  areas() {
    return http.get<AdminArea[]>("/api/admin/areas").then((res) => res.data);
  },

  createArea(input: { name: string; city?: string }) {
    return http.post<AdminArea>("/api/admin/areas", input).then((res) => res.data);
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
