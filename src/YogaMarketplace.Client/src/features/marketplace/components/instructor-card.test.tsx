import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { InstructorCard } from "@/features/marketplace/components/instructor-card";
import type { InstructorSummary } from "@/features/marketplace/types";

const base: InstructorSummary = {
  id: "p1",
  displayName: "Asha",
  bio: null,
  area: "Bandra",
  city: "Harbor",
  status: "Verified",
  modes: [{ mode: "Online", rate: 499 }],
  ratingAverage: null,
  reviewCount: 0,
  photoUrl: null,
  isVerified: true,
  isOutOfCity: true,
};

describe("InstructorCard", () => {
  it("renders an Online chip for out-of-city instructors", () => {
    render(
      <MemoryRouter>
        <InstructorCard instructor={base} />
      </MemoryRouter>,
    );
    expect(screen.getByText("Online · Harbor")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /View Asha/i })).toHaveAttribute(
      "href",
      expect.stringContaining("remote=1"),
    );
  });
});
