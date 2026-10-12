import { describe, expect, it } from "vitest";
import { parsePageBody } from "@/features/content/utils/page-blocks";

describe("parsePageBody", () => {
  it("reads paragraphs, headings and bullets", () => {
    const body = [
      "You pay when you book.",
      "The instructor then accepts.",
      "",
      "## Full refunds",
      "- The instructor declines.",
      "- You cancel early.",
      "Banks take 5 to 7 days.",
    ].join("\r\n");

    expect(parsePageBody(body)).toEqual([
      { kind: "paragraph", text: "You pay when you book. The instructor then accepts." },
      { kind: "heading", text: "Full refunds" },
      { kind: "list", items: ["The instructor declines.", "You cancel early."] },
      { kind: "paragraph", text: "Banks take 5 to 7 days." },
    ]);
  });

  it("keeps markup and bare markers as plain text", () => {
    expect(parsePageBody("<script>alert(1)</script>\n\n##\n- \n#not a heading")).toEqual([
      { kind: "paragraph", text: "<script>alert(1)</script>" },
      { kind: "paragraph", text: "## - #not a heading" },
    ]);
  });

  it("returns nothing for an empty body", () => {
    expect(parsePageBody("  \n\n ")).toEqual([]);
  });
});
