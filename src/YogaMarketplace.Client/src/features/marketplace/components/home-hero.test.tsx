import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { launch } from "@/constants/launch";
import { site } from "@/constants/site";
import { HomeHero } from "@/features/marketplace/components/home-hero";

function renderHero(props: Partial<Parameters<typeof HomeHero>[0]> = {}) {
  return render(
    <HomeHero city="Pune" areaName="Kothrud" banner={undefined} bannerLoading={false} onChangeArea={vi.fn()} {...props} />,
  );
}

describe("HomeHero", () => {
  it("shows the admin banner text", () => {
    renderHero({ banner: { title: "Find your calm", subtitle: "Verified instructors", offer: "20% off" } });
    expect(screen.getByRole("heading", { name: "Find your calm" })).toBeInTheDocument();
    expect(screen.getByText("Verified instructors")).toBeInTheDocument();
    expect(screen.getByText("20% off")).toBeInTheDocument();
  });

  it("falls back to the default copy when the banner is empty or failed to load", () => {
    renderHero({ banner: { title: "  ", subtitle: null, offer: null } });
    expect(screen.getByRole("heading", { name: site.tagline })).toBeInTheDocument();
    expect(screen.getByText(launch.heroChip)).toBeInTheDocument();
  });

  it("shows a placeholder while the banner loads", () => {
    renderHero({ bannerLoading: true });
    expect(screen.queryByRole("heading")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Loading")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Change area/ })).toBeInTheDocument();
  });
});
