import { describe, expect, it } from "vitest";
import { accountSchema } from "@/features/auth/schemas";

const newAccount = { accountKind: "new", phone: "9876543210", name: "Asha Rao", gender: "Female" } as const;

function errorPaths(input: Record<string, unknown>) {
  const result = accountSchema.safeParse(input);
  return result.success ? [] : result.error.issues.map((issue) => issue.path.join("."));
}

describe("account form consent", () => {
  it("blocks a new account until Terms and Privacy are accepted", () => {
    expect(errorPaths({ ...newAccount, acceptedPolicies: false })).toEqual(["acceptedPolicies"]);
    expect(errorPaths(newAccount)).toEqual(["acceptedPolicies"]);
    expect(errorPaths({ ...newAccount, acceptedPolicies: true })).toEqual([]);
  });

  it("does not ask an existing account to tick the box", () => {
    expect(errorPaths({ accountKind: "existing", phone: "9876543210" })).toEqual([]);
  });
});
