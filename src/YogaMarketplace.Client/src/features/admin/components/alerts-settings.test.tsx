import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ErrorAlertSettings } from "@/features/admin/components/error-alert-settings";
import { NotificationSettings } from "@/features/admin/components/notification-settings";
import type { AdminSettings, UpdateSettingsInput } from "@/features/admin/types";
import { ApiError } from "@/services/http/api-error";

const { settings, updateSettings } = vi.hoisted(() => ({ settings: vi.fn(), updateSettings: vi.fn() }));

vi.mock("@/features/admin/api/admin-api", () => ({ adminApi: { settings, updateSettings, sendTestAlert: vi.fn() } }));

const loaded = {
  version: 1,
  emailAlertsEnabled: false,
  errorAlertEmail: null,
  errorAlertThrottleMinutes: 60,
  errorAlertCheckMinutes: 5,
  errorEventRetentionDays: 30,
  emailNotificationsEnabled: false,
  notificationMaxAttempts: 3,
} as unknown as AdminSettings;

function renderAlertsTab() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <ErrorAlertSettings />
      <NotificationSettings />
    </QueryClientProvider>,
  );
}

describe("Alerts tab", () => {
  beforeEach(() => {
    settings.mockReset().mockResolvedValue(loaded);
    updateSettings.mockReset().mockImplementation(async ({ version, ...changes }: UpdateSettingsInput) => ({
      ...loaded,
      ...changes,
      version: version + 1,
    }));
  });

  it("saving one card keeps the unsaved edits in the other card", async () => {
    renderAlertsTab();
    const tries = await screen.findByLabelText("Tries per email");
    fireEvent.change(tries, { target: { value: "5" } });
    fireEvent.change(screen.getByLabelText("Quiet period per error (minutes)"), { target: { value: "90" } });

    fireEvent.click(screen.getByRole("button", { name: "Save error alerts" }));

    await waitFor(() => expect(updateSettings).toHaveBeenCalledWith({ version: 1, errorAlertThrottleMinutes: 90 }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Save error alerts" })).toBeDisabled());
    expect(screen.getByLabelText("Quiet period per error (minutes)")).toHaveValue("90");
    expect(screen.getByLabelText("Tries per email")).toHaveValue("5");
    const saveEmails = screen.getByRole("button", { name: "Save booking emails" });
    expect(saveEmails).toBeEnabled();

    fireEvent.click(saveEmails);

    await waitFor(() => expect(updateSettings).toHaveBeenLastCalledWith({ version: 2, notificationMaxAttempts: 5 }));
  });

  it("a conflict in one card reloads the latest values but keeps the other card's unsaved edits", async () => {
    updateSettings.mockReset().mockRejectedValue(new ApiError("Another admin saved these settings first.", 409));
    renderAlertsTab();
    fireEvent.change(await screen.findByLabelText("Tries per email"), { target: { value: "5" } });
    fireEvent.change(screen.getByLabelText("Quiet period per error (minutes)"), { target: { value: "90" } });
    settings.mockResolvedValue({ ...loaded, version: 2, errorAlertThrottleMinutes: 45, emailNotificationsEnabled: true });

    fireEvent.click(screen.getByRole("button", { name: "Save error alerts" }));

    await waitFor(() => expect(screen.getByLabelText("Quiet period per error (minutes)")).toHaveValue("45"));
    expect(screen.getByLabelText("Emails to customers and instructors")).toBeChecked();
    expect(screen.getByLabelText("Tries per email")).toHaveValue("5");
    expect(screen.getByRole("button", { name: "Save booking emails" })).toBeEnabled();
  });
});
