import { render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { SiteFooter } from "@/components/common/site-footer";

function footerLinks() {
  return within(screen.getByRole("navigation", { name: "More" }))
    .getAllByRole("link")
    .map((link) => [link.textContent, link.getAttribute("href")]);
}

describe("SiteFooter", () => {
  it("links to packs and all three policy pages", () => {
    render(
      <MemoryRouter>
        <SiteFooter />
      </MemoryRouter>,
    );

    expect(footerLinks()).toEqual([
      ["Session packs", "/packs"],
      ["Refund policy", "/refund-policy"],
      ["Terms", "/terms"],
      ["Privacy", "/privacy"],
    ]);
  });

  it("shows only the policy pages without packs", () => {
    render(
      <MemoryRouter>
        <SiteFooter withPacks={false} />
      </MemoryRouter>,
    );

    expect(footerLinks()).toEqual([
      ["Refund policy", "/refund-policy"],
      ["Terms", "/terms"],
      ["Privacy", "/privacy"],
    ]);
  });
});
