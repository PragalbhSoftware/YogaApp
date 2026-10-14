import { renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { useStepBackWhenEmpty } from "@/features/admin/hooks/use-step-back-when-empty";
import type { PagedList } from "@/features/admin/types";

function list(page: number, items: number, total: number): PagedList<unknown> {
  return { page, pageSize: 20, total, items: Array.from({ length: items }, (_, i) => i) };
}

describe("useStepBackWhenEmpty", () => {
  it("steps back one page when the current page empties", () => {
    const goTo = vi.fn();
    renderHook(() => useStepBackWhenEmpty(list(3, 0, 40), 3, goTo));
    expect(goTo).toHaveBeenCalledExactlyOnceWith(2);
  });

  it("goes to the last page with rows when several pages emptied", () => {
    const goTo = vi.fn();
    renderHook(() => useStepBackWhenEmpty(list(4, 0, 15), 4, goTo));
    expect(goTo).toHaveBeenCalledExactlyOnceWith(1);
  });

  it("stays put on page 1, on a page with rows, while loading, or with data from another page", () => {
    const goTo = vi.fn();
    renderHook(() => useStepBackWhenEmpty(list(1, 0, 0), 1, goTo));
    renderHook(() => useStepBackWhenEmpty(list(2, 1, 21), 2, goTo));
    renderHook(() => useStepBackWhenEmpty(undefined, 2, goTo));
    renderHook(() => useStepBackWhenEmpty(list(3, 0, 40), 2, goTo));
    expect(goTo).not.toHaveBeenCalled();
  });
});
