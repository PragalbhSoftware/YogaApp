import { describe, expect, it } from "vitest";
import { listParams } from "@/features/admin/api/admin-api";
import { readPage } from "@/features/admin/hooks/use-list-params";
import { canMarkPaid, pageCount, rangeLabel, statusTone } from "@/features/admin/list-utils";

describe("admin paging", () => {
  it("counts at least one page", () => {
    expect(pageCount(0, 20)).toBe(1);
    expect(pageCount(20, 20)).toBe(1);
    expect(pageCount(21, 20)).toBe(2);
  });

  it("labels the rows on the current page", () => {
    expect(rangeLabel(0, 1, 20, "users")).toBe("No users");
    expect(rangeLabel(45, 1, 20, "users")).toBe("1–20 of 45 users");
    expect(rangeLabel(45, 3, 20, "users")).toBe("41–45 of 45 users");
  });

  it("reads only whole page numbers above one from the URL", () => {
    expect(readPage(null)).toBe(1);
    expect(readPage("3")).toBe(3);
    expect(readPage("0")).toBe(1);
    expect(readPage("-2")).toBe(1);
    expect(readPage("2.5")).toBe(1);
    expect(readPage("abc")).toBe(1);
  });

  it("drops empty filters from list requests", () => {
    expect(listParams({ status: undefined, q: "", role: "Admin", page: 2 })).toEqual({ role: "Admin", page: 2 });
  });
});

describe("admin statuses", () => {
  it("tones statuses that need work differently from settled ones", () => {
    expect(statusTone("Pending")).toBe("attention");
    expect(statusTone("Paid")).toBe("positive");
    expect(statusTone("Failed")).toBe("negative");
    expect(statusTone("SomethingNew")).toBe("neutral");
  });

  it("lets only pending or exported payouts be marked paid", () => {
    expect(canMarkPaid({ status: "Pending" })).toBe(true);
    expect(canMarkPaid({ status: "Exported" })).toBe(true);
    expect(canMarkPaid({ status: "Paid" })).toBe(false);
  });
});
