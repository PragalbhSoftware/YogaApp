import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { JobCard } from "@/features/admin/components/job-card";
import type { AdminJob } from "@/features/admin/types";

function job(overrides: Partial<AdminJob> = {}): AdminJob {
  return {
    id: "j1",
    type: "payments.reconcile",
    payload: "2026-10-12",
    status: "Failed",
    runAt: "2026-10-13T00:30:00Z",
    attempts: 5,
    maxAttempts: 5,
    lastError: "Razorpay timed out.",
    createdAt: "2026-10-12T00:30:00Z",
    completedAt: "2026-10-13T01:00:00Z",
    recurring: false,
    ...overrides,
  };
}

describe("JobCard", () => {
  it("shows a failed job's error and retries it", () => {
    const onRetry = vi.fn();
    render(<JobCard job={job()} retrying={false} onRetry={onRetry} />);

    expect(screen.getByRole("heading", { name: "Payment reconciliation" })).toBeInTheDocument();
    expect(screen.getByText("Razorpay timed out.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Retry now" }));
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it("disables retry while it is being sent", () => {
    render(<JobCard job={job()} retrying onRetry={vi.fn()} />);
    expect(screen.getByRole("button", { name: "Retrying…" })).toBeDisabled();
  });

  it("offers no retry for a job that has not failed", () => {
    render(
      <JobCard
        job={job({ status: "Pending", attempts: 0, lastError: null, completedAt: null, recurring: true, type: "test.other" })}
        retrying={false}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.getByRole("heading", { name: "test.other" })).toBeInTheDocument();
    expect(screen.getByText("Scheduled")).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});
