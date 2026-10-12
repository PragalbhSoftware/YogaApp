import { describe, expect, it } from "vitest";
import { settingsConflictMessage, settingsSaveError } from "@/features/admin/utils";
import { ApiError } from "@/services/http/api-error";

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
