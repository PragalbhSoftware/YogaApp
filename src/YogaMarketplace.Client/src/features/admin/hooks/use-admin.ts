import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { adminApi } from "@/features/admin/api/admin-api";
import type { BookingListQuery, UpdateSettingsInput, UserListQuery } from "@/features/admin/types";
import { ApiError } from "@/services/http/api-error";

export function useAdminSummary() {
  return useQuery({
    queryKey: ["admin-summary"],
    queryFn: adminApi.summary,
  });
}

export function useAdminProviders(status: string) {
  return useQuery({
    queryKey: ["admin-providers", status],
    queryFn: () => adminApi.providers(status),
  });
}

export function useVerifyProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => adminApi.verifyProvider(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["admin-providers"] });
      void queryClient.invalidateQueries({ queryKey: ["admin-users"] });
      void queryClient.invalidateQueries({ queryKey: ["admin-user"] });
      void queryClient.invalidateQueries({ queryKey: ["instructors"] });
    },
  });
}

export function useRejectProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; reason?: string }) => adminApi.rejectProvider(input.id, input.reason),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["admin-providers"] });
      void queryClient.invalidateQueries({ queryKey: ["admin-users"] });
      void queryClient.invalidateQueries({ queryKey: ["admin-user"] });
      void queryClient.invalidateQueries({ queryKey: ["instructors"] });
    },
  });
}

export function useAdminUsers(query: UserListQuery) {
  return useQuery({
    queryKey: ["admin-users", query.q ?? "", query.role ?? ""],
    queryFn: () => adminApi.users(query),
  });
}

export function useAdminUser(id: string | undefined) {
  return useQuery({
    queryKey: ["admin-user", id],
    queryFn: () => adminApi.user(id ?? ""),
    enabled: Boolean(id),
  });
}

export function useSetUserBlocked() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; blocked: boolean; reason?: string }) =>
      input.blocked ? adminApi.blockUser(input.id, input.reason ?? "") : adminApi.unblockUser(input.id, input.reason),
    onSuccess: (user) => {
      queryClient.setQueryData(["admin-user", user.id], user);
      void queryClient.invalidateQueries({ queryKey: ["admin-users"] });
      void queryClient.invalidateQueries({ queryKey: ["admin-providers"] });
      void queryClient.invalidateQueries({ queryKey: ["instructors"] });
    },
  });
}

export function useAdminBookings(query: BookingListQuery) {
  return useQuery({
    queryKey: ["admin-bookings", query.status ?? "", query.from ?? "", query.to ?? ""],
    queryFn: () => adminApi.bookings(query),
  });
}

export function useAdminBooking(id: string | undefined) {
  return useQuery({
    queryKey: ["admin-booking", id],
    queryFn: () => adminApi.booking(id ?? ""),
    enabled: Boolean(id),
  });
}

export function useAdminCancelBooking() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; reason: string }) => adminApi.cancelBooking(input.id, input.reason),
    onSuccess: (booking) => {
      queryClient.setQueryData(["admin-booking", booking.id], booking);
      void queryClient.invalidateQueries({ queryKey: ["admin-bookings"] });
      void queryClient.invalidateQueries({ queryKey: ["admin-payments"] });
      void queryClient.invalidateQueries({ queryKey: ["admin-summary"] });
    },
  });
}

export function useAdminPayments(status?: string, enabled = true) {
  return useQuery({
    queryKey: ["admin-payments", status ?? ""],
    queryFn: () => adminApi.payments(status),
    enabled,
  });
}

export function useAdminPayouts(status?: string, enabled = true) {
  return useQuery({
    queryKey: ["admin-payouts", status ?? ""],
    queryFn: () => adminApi.payouts(status),
    enabled,
  });
}

export function useExportPayouts() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => adminApi.exportPendingPayouts(),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["admin-payouts"] });
      void queryClient.invalidateQueries({ queryKey: ["admin-summary"] });
    },
  });
}

export function useDownloadPayoutsCsv() {
  return useMutation({
    mutationFn: (status: string) => adminApi.downloadPayoutsCsv(status),
  });
}

export function useMarkPayoutPaid() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => adminApi.markPayoutPaid(id),
    onSuccess: (payout) => {
      void queryClient.invalidateQueries({ queryKey: ["admin-payouts"] });
      void queryClient.invalidateQueries({ queryKey: ["admin-summary"] });
      void queryClient.invalidateQueries({ queryKey: ["admin-booking", payout.bookingId] });
    },
  });
}

export function useAdminAreas() {
  return useQuery({
    queryKey: ["admin-areas"],
    queryFn: adminApi.areas,
  });
}

export function useCreateArea() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: adminApi.createArea,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["admin-areas"] });
      void queryClient.invalidateQueries({ queryKey: ["areas"] });
    },
  });
}

export function useUpdateArea() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; name?: string; isActive?: boolean }) =>
      adminApi.updateArea(input.id, { name: input.name, isActive: input.isActive }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["admin-areas"] });
      void queryClient.invalidateQueries({ queryKey: ["areas"] });
      void queryClient.invalidateQueries({ queryKey: ["instructors"] });
    },
  });
}

export function useAdminCategories() {
  return useQuery({
    queryKey: ["admin-categories"],
    queryFn: adminApi.categories,
  });
}

export function useRenameCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; name: string }) => adminApi.renameCategory(input.id, input.name),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["admin-categories"] });
    },
  });
}

export function useAdminSettings() {
  return useQuery({
    queryKey: ["admin-settings"],
    queryFn: adminApi.settings,
  });
}

export function useSettingsAudit() {
  return useQuery({
    queryKey: ["admin-settings-audit"],
    queryFn: adminApi.settingsAudit,
  });
}

/** On a 409 another admin saved first, so the form reloads the latest values before the next try. */
export function useUpdateSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateSettingsInput) => adminApi.updateSettings(input),
    onSuccess: (settings) => {
      queryClient.setQueryData(["admin-settings"], settings);
      for (const queryKey of settingsReaders) {
        void queryClient.invalidateQueries({ queryKey });
      }
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        void queryClient.invalidateQueries({ queryKey: ["admin-settings"] });
        void queryClient.invalidateQueries({ queryKey: ["admin-settings-audit"] });
      }
    },
  });
}

const settingsReaders = [["admin-settings-audit"], ["policy"], ["site-banner"], ["areas"], ["instructors"]];
