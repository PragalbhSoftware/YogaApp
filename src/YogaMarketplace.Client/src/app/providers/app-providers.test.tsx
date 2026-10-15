import { act, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AppProviders } from "@/app/providers/app-providers";
import { refreshSession } from "@/services/http/http-client";

vi.mock("@/services/http/http-client", () => ({ refreshSession: vi.fn() }));
vi.mock("@/app/router/app-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/app/router/app-router")>()),
  AppRouter: () => <p>app routes</p>,
}));

describe("AppProviders", () => {
  it("shows the loading screen until the session is restored, then the routes", async () => {
    localStorage.setItem("ym.signedIn", "1");
    let finish!: () => void;
    vi.mocked(refreshSession).mockReturnValue(
      new Promise((resolve) => {
        finish = () => resolve({ ok: false, error: new Error("401") });
      }),
    );

    render(<AppProviders />);

    expect(screen.getByRole("progressbar")).toBeInTheDocument();
    expect(screen.queryByText("app routes")).not.toBeInTheDocument();

    await act(async () => finish());

    expect(screen.getByText("app routes")).toBeInTheDocument();
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });
});
