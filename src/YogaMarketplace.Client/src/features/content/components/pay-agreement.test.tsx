import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { PayAgreement } from "@/features/content/components/pay-agreement";

describe("PayAgreement", () => {
  it("links to the Terms and Refund policy pages in the same tab", () => {
    render(
      <MemoryRouter>
        <PayAgreement />
      </MemoryRouter>,
    );

    const terms = screen.getByRole("link", { name: "Terms" });
    const refund = screen.getByRole("link", { name: "Refund policy" });
    expect(terms).toHaveAttribute("href", "/terms");
    expect(refund).toHaveAttribute("href", "/refund-policy");
    expect(terms).not.toHaveAttribute("target");
    expect(refund).not.toHaveAttribute("target");
  });
});
