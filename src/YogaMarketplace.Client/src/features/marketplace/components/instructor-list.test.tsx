import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { InstructorList } from "@/features/marketplace/components/instructor-list";
import type { InstructorSummary } from "@/features/marketplace/types";

const online: InstructorSummary = {
  id: "p1",
  displayName: "Asha",
  bio: null,
  area: "Elsewhere",
  city: "Other",
  status: "Verified",
  modes: [{ mode: "Online", rate: 499 }],
  ratingAverage: null,
  reviewCount: 0,
  isVerified: true,
  isOutOfCity: true,
};

describe("InstructorList empty states", () => {
  it("shows a city empty state with an Online action when nothing is listed", () => {
    render(
      <MemoryRouter>
        <InstructorList
          cityName="Harbor"
          mode=""
          modeLabel="any mode"
          instructors={[]}
          isLoading={false}
          isError={false}
          error={null}
          onRetry={vi.fn()}
          onShowOnline={vi.fn()}
        />
      </MemoryRouter>,
    );
    expect(screen.getByText("No instructors in Harbor yet")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Show Online" })).toBeInTheDocument();
  });

  it("hides Show Online when already filtering Online", () => {
    render(
      <MemoryRouter>
        <InstructorList
          cityName="Harbor"
          mode="Online"
          modeLabel="Online"
          instructors={[]}
          isLoading={false}
          isError={false}
          error={null}
          onRetry={vi.fn()}
          onShowOnline={vi.fn()}
        />
      </MemoryRouter>,
    );
    expect(screen.getByText("No instructors in Harbor yet")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Show Online" })).not.toBeInTheDocument();
  });

  it("shows the in-person empty note above online-only results", () => {
    render(
      <MemoryRouter>
        <InstructorList
          cityName="Harbor"
          mode=""
          modeLabel="any mode"
          instructors={[online]}
          isLoading={false}
          isError={false}
          error={null}
          onRetry={vi.fn()}
          onShowOnline={vi.fn()}
        />
      </MemoryRouter>,
    );
    expect(screen.getByText("No in-person instructors in this city yet")).toBeInTheDocument();
    expect(screen.getByText("Online · Other")).toBeInTheDocument();
  });

  it("shows Showing N without implying a total count", () => {
    const local: InstructorSummary = { ...online, isOutOfCity: false, area: "Bandra", city: "Harbor" };
    render(
      <MemoryRouter>
        <InstructorList
          cityName="Harbor"
          mode=""
          modeLabel="any mode"
          instructors={[local]}
          isLoading={false}
          isError={false}
          error={null}
          onRetry={vi.fn()}
          onShowOnline={vi.fn()}
        />
      </MemoryRouter>,
    );
    expect(screen.getByText("Showing 1")).toBeInTheDocument();
  });
});
