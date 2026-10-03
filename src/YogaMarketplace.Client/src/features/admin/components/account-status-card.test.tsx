import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AccountStatusCard } from "@/features/admin/components/account-status-card";
import type { AdminUserDetail } from "@/features/admin/types";

function account(overrides: Partial<AdminUserDetail> = {}): AdminUserDetail {
  return {
    id: "u1",
    name: "Asha",
    phone: "+919811111111",
    gender: null,
    email: null,
    role: "Customer",
    createdAt: "2026-10-01T05:00:00Z",
    provider: null,
    isBlocked: false,
    blockedAt: null,
    blockedReason: null,
    blockHistory: [],
    ...overrides,
  };
}

describe("AccountStatusCard", () => {
  it("requires a reason before blocking", async () => {
    const onChange = vi.fn();
    render(<AccountStatusCard user={account()} busy={false} onChange={onChange} />);

    fireEvent.click(screen.getByRole("button", { name: "Block account" }));
    fireEvent.click(screen.getByRole("button", { name: "Block" }));
    expect(await screen.findByText("Give a reason of at least 5 characters.")).toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText("Reason"), { target: { value: "  Repeated no-shows  " } });
    fireEvent.click(screen.getByRole("button", { name: "Block" }));
    await waitFor(() => expect(onChange).toHaveBeenCalledWith(true, "Repeated no-shows"));
  });

  it("unblocks without a reason", async () => {
    const onChange = vi.fn();
    render(
      <AccountStatusCard
        user={account({ isBlocked: true, blockedAt: "2026-10-02T05:00:00Z", blockedReason: "Spam" })}
        busy={false}
        onChange={onChange}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Unblock account" }));
    fireEvent.click(screen.getByRole("button", { name: "Unblock" }));
    await waitFor(() => expect(onChange).toHaveBeenCalledWith(false, undefined));
  });

  it("offers no block action for admins", () => {
    render(<AccountStatusCard user={account({ role: "Admin" })} busy={false} onChange={vi.fn()} />);
    expect(screen.queryByRole("button", { name: "Block account" })).not.toBeInTheDocument();
  });
});
