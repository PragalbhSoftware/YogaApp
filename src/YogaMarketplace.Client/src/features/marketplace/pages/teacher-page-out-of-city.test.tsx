import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TeacherPage } from "@/features/marketplace/pages/teacher-page";
import { useCityStore } from "@/stores/city-store";

vi.mock("@/features/marketplace/hooks/use-instructors", () => ({
  useInstructor: () => ({
    data: {
      id: "p1",
      displayName: "Asha",
      bio: null,
      age: null,
      area: "Elsewhere",
      city: "Other",
      cityId: "other-city",
      status: "Verified",
      modes: [
        { mode: "Home", rate: 899 },
        { mode: "Online", rate: 499 },
      ],
      studioAddress: null,
      ratingAverage: null,
      reviewCount: 0,
      areaOpen: true,
      photoUrls: [],
      introVideoId: null,
      certificates: [],
      isVerified: true,
      trial: null,
    },
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
  }),
  useInstructorReviews: () => ({
    data: [],
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
  }),
}));

vi.mock("@/features/marketplace/components/availability-section", () => ({
  AvailabilitySection: () => <div>Availability</div>,
}));

vi.mock("@/features/marketplace/components/instructor-gallery", () => ({
  InstructorGallery: () => null,
}));

vi.mock("@/features/marketplace/components/instructor-about", () => ({
  InstructorAbout: () => null,
}));

vi.mock("@/features/marketplace/components/intro-video", () => ({
  IntroVideo: () => null,
}));

vi.mock("@/features/marketplace/components/instructor-credentials", () => ({
  InstructorCredentials: () => null,
}));

vi.mock("@/features/marketplace/components/review-list", () => ({
  ReviewList: () => null,
}));

describe("teacher page out of city", () => {
  beforeEach(() => {
    useCityStore.getState().setCityId("c1");
  });

  it("shows Online only when the provider city differs without remote=1", async () => {
    render(
      <MemoryRouter initialEntries={["/teachers/p1"]}>
        <Routes>
          <Route path="/teachers/:id" element={<TeacherPage />} />
        </Routes>
      </MemoryRouter>,
    );
    await waitFor(() => expect(screen.getByText("Online · Other")).toBeInTheDocument());
    expect(screen.queryByText("Home")).not.toBeInTheDocument();
    expect(screen.getAllByText("Online").length).toBeGreaterThan(0);
  });
});
