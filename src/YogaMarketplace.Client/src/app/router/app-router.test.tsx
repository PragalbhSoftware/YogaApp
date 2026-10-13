import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it } from "vitest";
import { AppRouter } from "@/app/router/app-router";
import { routes } from "@/constants/routes";
import { useAuthStore } from "@/stores/auth-store";

describe("policy pages", () => {
  beforeEach(() => useAuthStore.getState().signOut());

  it.each([
    [null, routes.terms],
    ["Customer", routes.privacy],
    ["Provider", routes.terms],
    ["Provider", routes.privacy],
    ["Admin", routes.refundPolicy],
  ])("are open to %s at %s without a redirect", async (role, path) => {
    if (role) {
      useAuthStore.getState().signIn("token", { id: "u1", name: "Test", phone: "+919000000000", gender: null, role });
    }
    window.history.pushState({}, "", path);

    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <AppRouter />
      </QueryClientProvider>,
    );

    expect(await screen.findByText("Yoga Marketplace · India")).toBeInTheDocument();
    expect(window.location.pathname).toBe(path);
  });
});
