import { render, screen } from "@testing-library/react";
import { act } from "react";
import { createMemoryRouter, RouterProvider, Outlet } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AppErrorBoundary } from "@/app/router/app-error-boundary";
import { NotFoundPage } from "@/app/router/not-found-page";

function Broken(): never {
  throw new Error("render failed");
}

function renderAt(path: string) {
  const router = createMemoryRouter(
    [
      {
        element: (
          <AppErrorBoundary>
            <Outlet />
          </AppErrorBoundary>
        ),
        children: [
          { path: "/broken", element: <Broken /> },
          { path: "/fine", element: <p>fine page</p> },
          { path: "*", element: <NotFoundPage /> },
        ],
      },
    ],
    { initialEntries: [path] },
  );
  render(<RouterProvider router={router} />);
  return router;
}

describe("AppErrorBoundary", () => {
  beforeEach(() => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows a recovery screen instead of a blank page", () => {
    renderAt("/broken");

    expect(screen.getByRole("alert")).toHaveTextContent("This page hit a problem");
    expect(screen.getByRole("button", { name: "Try again" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Go home" })).toHaveAttribute("href", "/");
  });

  it("recovers when the user navigates away", async () => {
    const router = renderAt("/broken");

    await act(() => router.navigate("/fine"));

    expect(screen.getByText("fine page")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});

describe("NotFoundPage", () => {
  it("offers a way home for unknown links", () => {
    renderAt("/no-such-page");

    expect(screen.getByRole("heading", { name: "We couldn’t find that page" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Go home" })).toHaveAttribute("href", "/");
  });
});
