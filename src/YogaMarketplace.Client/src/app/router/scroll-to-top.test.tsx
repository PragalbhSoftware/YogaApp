import { render } from "@testing-library/react";
import { act } from "react";
import { createMemoryRouter, Outlet, RouterProvider } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ScrollToTop } from "@/app/router/scroll-to-top";

function renderAt(...entries: string[]) {
  const router = createMemoryRouter(
    [
      {
        element: (
          <>
            <ScrollToTop />
            <Outlet />
          </>
        ),
        children: [
          { path: "/", element: <p>home</p> },
          { path: "/book", element: <p>checkout</p> },
          { path: "/terms", element: <p>terms</p> },
        ],
      },
    ],
    { initialEntries: entries, initialIndex: entries.length - 1 },
  );
  render(<RouterProvider router={router} />);
  return router;
}

describe("ScrollToTop", () => {
  let scrollTo: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    scrollTo = vi.spyOn(window, "scrollTo").mockImplementation(() => undefined);
  });
  afterEach(() => vi.restoreAllMocks());

  it("does not scroll on the first page load", () => {
    renderAt("/book");

    expect(scrollTo).not.toHaveBeenCalled();
  });

  it("scrolls a newly opened page to the top", async () => {
    const router = renderAt("/");

    await act(() => router.navigate("/book"));

    expect(scrollTo).toHaveBeenCalledExactlyOnceWith(0, 0);
  });

  it("scrolls to the top when a redirect replaces the page", async () => {
    const router = renderAt("/");

    await act(() => router.navigate("/terms", { replace: true }));

    expect(scrollTo).toHaveBeenCalledExactlyOnceWith(0, 0);
  });

  it("keeps the position on Back", async () => {
    const router = renderAt("/book", "/terms");

    await act(() => router.navigate(-1));

    expect(router.state.location.pathname).toBe("/book");
    expect(scrollTo).not.toHaveBeenCalled();
  });

  it("does not scroll when only the query string changes, even right after Back", async () => {
    const router = renderAt("/book", "/terms");
    await act(() => router.navigate(-1));

    await act(() => router.navigate("/book?pay=pay", { replace: true }));
    await act(() => router.navigate("/book?pay=credit"));

    expect(scrollTo).not.toHaveBeenCalled();
  });
});
