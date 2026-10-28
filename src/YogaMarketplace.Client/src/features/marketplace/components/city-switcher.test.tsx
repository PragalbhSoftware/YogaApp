import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CitySwitcher } from "@/features/marketplace/components/city-switcher";
import { profileApi } from "@/features/profile/api/profile-api";
import { useAreaStore } from "@/stores/area-store";
import { useAuthStore } from "@/stores/auth-store";
import { useCityStore } from "@/stores/city-store";

vi.mock("@/features/marketplace/hooks/use-cities", () => ({
  useCities: () => ({
    data: [
      { id: "c1", name: "North", stateId: "s1", slug: "n", displayOrder: 1 },
      { id: "c2", name: "South", stateId: "s1", slug: "s", displayOrder: 2 },
    ],
    isSuccess: true,
  }),
}));

vi.mock("@/features/profile/api/profile-api", () => ({
  profileApi: { savePreferredCity: vi.fn() },
}));

describe("CitySwitcher", () => {
  beforeEach(() => {
    vi.mocked(profileApi.savePreferredCity).mockReset();
    vi.mocked(profileApi.savePreferredCity).mockResolvedValue({} as never);
    useCityStore.getState().setCityId("c1");
    useAreaStore.getState().setArea("Harbour");
    useAuthStore.getState().signOut();
  });

  it("clears the neighbourhood when the city changes", async () => {
    render(<CitySwitcher />);
    fireEvent.click(screen.getByRole("button", { name: /Change city. Currently North/i }));
    fireEvent.click(await screen.findByRole("button", { name: "South" }));

    await waitFor(() => expect(useCityStore.getState().cityId).toBe("c2"));
    expect(useAreaStore.getState().areaName).toBeNull();
  });

  it("keeps the new city when the profile save fails", async () => {
    useAuthStore.getState().signIn("token", {
      id: "u1",
      name: "Pat",
      phone: "+919999999999",
      gender: "Other",
      role: "Customer",
      consentRequired: false,
    });
    vi.mocked(profileApi.savePreferredCity).mockRejectedValue(new Error("offline"));

    render(<CitySwitcher />);
    fireEvent.click(screen.getByRole("button", { name: /Change city. Currently North/i }));
    fireEvent.click(await screen.findByRole("button", { name: "South" }));

    await waitFor(() => expect(useCityStore.getState().cityId).toBe("c2"));
    expect(useAreaStore.getState().areaName).toBeNull();
    expect(useCityStore.getState().preferredCitySyncPending).toBe(true);
  });
});
