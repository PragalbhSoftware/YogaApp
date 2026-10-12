import { describe, expect, it } from "vitest";
import { paymentTitle, settingsConflictMessage, settingsSaveError } from "@/features/admin/utils";
import { ApiError } from "@/services/http/api-error";

describe("paymentTitle", () => {
  it("names the pack for a pack sale and the instructor for a booking", () => {
    expect(paymentTitle({ kind: "Pack", providerName: null, packName: "8 sessions" })).toBe("8 sessions");
    expect(paymentTitle({ kind: "Booking", providerName: "Ananya Rao", packName: null })).toBe("Ananya Rao");
  });
});

describe("settingsSaveError", () => {
  it("tells the admin their edits were reloaded on a 409", () => {
    expect(settingsSaveError(new ApiError("Another admin saved these settings first.", 409))).toBe(settingsConflictMessage);
    expect(settingsConflictMessage).toMatch(/unsaved edits were discarded/);
  });

  it("shows the API message for other errors", () => {
    expect(settingsSaveError(new ApiError("Commission must be between 0 and 100.", 400))).toBe(
      "Commission must be between 0 and 100.",
    );
  });
});
