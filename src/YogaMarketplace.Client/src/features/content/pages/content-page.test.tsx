import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { Link, MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { routes } from "@/constants/routes";
import { TermsPage } from "@/features/content/pages/content-page";
import type { ContentPage } from "@/features/content/types";

const { page } = vi.hoisted(() => ({ page: vi.fn() }));

vi.mock("@/features/content/api/content-api", () => ({ contentApi: { page } }));

function termsPage(body: string): ContentPage {
  return { slug: "terms", title: "Terms of use", body, version: 1, updatedAt: "2026-10-01T06:30:00Z" };
}

function renderAt(initialEntries: string[]) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={initialEntries} initialIndex={initialEntries.length - 1}>
        <Routes>
          <Route path={routes.home} element={<p>Home page</p>} />
          <Route
            path="/checkout"
            element={
              <>
                <p>Checkout page</p>
                <Link to={routes.terms}>Terms</Link>
              </>
            }
          />
          <Route path={routes.terms} element={<TermsPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("ContentPage", () => {
  beforeEach(() => {
    page.mockReset();
    page.mockResolvedValue(termsPage("Use the platform fairly."));
  });

  it("goes home from Back when the page was opened directly", async () => {
    renderAt([routes.terms]);

    expect(await screen.findByRole("heading", { name: "Terms of use" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Back" }));

    expect(screen.getByText("Home page")).toBeInTheDocument();
  });

  it("returns to the previous page from Back when opened inside the app", async () => {
    renderAt(["/checkout"]);

    fireEvent.click(screen.getByRole("link", { name: "Terms" }));
    expect(await screen.findByRole("heading", { name: "Terms of use" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Back" }));

    expect(screen.getByText("Checkout page")).toBeInTheDocument();
  });

  it("shows the Back button while the page is still loading", () => {
    page.mockReturnValue(new Promise(() => {}));
    renderAt([routes.terms]);

    expect(screen.getByRole("button", { name: "Back" })).toBeInTheDocument();
  });

  it("loads the latest text on every visit so admin edits show without a deploy", async () => {
    page.mockResolvedValueOnce(termsPage("Old wording.")).mockResolvedValueOnce(termsPage("Edited by admin."));
    renderAt(["/checkout"]);

    fireEvent.click(screen.getByRole("link", { name: "Terms" }));
    expect(await screen.findByText("Old wording.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Back" }));
    fireEvent.click(screen.getByRole("link", { name: "Terms" }));

    expect(await screen.findByText("Edited by admin.")).toBeInTheDocument();
    expect(page).toHaveBeenCalledTimes(2);
  });
});
