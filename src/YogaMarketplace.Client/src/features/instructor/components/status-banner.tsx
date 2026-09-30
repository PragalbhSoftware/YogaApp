import { Alert } from "@mui/material";
import type { InstructorProfile } from "@/features/instructor/types";

type StatusBannerProps = {
  profile: InstructorProfile;
};

export function StatusBanner({ profile }: StatusBannerProps) {
  const status = profile.status.toLowerCase();
  if (status === "pending") {
    return (
      <Alert severity="info">
        Your profile is under review. You can add, edit, or remove slots after an admin verifies
        you. Customers will see your times once you are approved.
      </Alert>
    );
  }
  if (status === "rejected") {
    return (
      <Alert severity="warning">
        This profile was not approved
        {profile.rejectionReason ? `: ${profile.rejectionReason}` : "."} Slot changes stay locked
        until an admin verifies you.
      </Alert>
    );
  }
  return null;
}
