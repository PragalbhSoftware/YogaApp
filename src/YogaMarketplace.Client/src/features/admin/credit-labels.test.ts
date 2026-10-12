import { describe, expect, it } from "vitest";
import { payBasisHint } from "@/features/admin/credit-labels";

describe("payBasisHint", () => {
  it("warns that paying on the normal session price can pay instructors more than the customer paid", () => {
    expect(payBasisHint("SessionPrice")).toMatch(/^Instructors may earn more per credit session than the customer paid\./);
    expect(payBasisHint("PackPerSession")).not.toMatch(/earn more/);
  });
});
