import { render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { InstructorLayout } from "@/app/layouts/instructor-layout";
import { routes } from "@/constants/routes";

describe("InstructorLayout", () => {
  it("shows the footer with the three policy pages and no packs link", () => {
    render(
      <MemoryRouter initialEntries={[routes.instructor]}>
        <Routes>
          <Route element={<InstructorLayout />}>
            <Route path={routes.instructor} element={<p>schedule</p>} />
          </Route>
        </Routes>
      </MemoryRouter>,
    );

    const footer = within(screen.getByRole("navigation", { name: "More" }));
    expect(footer.getAllByRole("link").map((link) => link.getAttribute("href"))).toEqual([
      routes.refundPolicy,
      routes.terms,
      routes.privacy,
    ]);
  });
});
