import { Avatar, Button, Chip } from "@mui/material";
import { LogOut } from "lucide-react";
import { toast } from "sonner";
import { usePageTitle } from "@/hooks/use-page-title";
import { PhoneText } from "@/components/common/phone-text";
import { ErrorState } from "@/components/common/error-state";
import { PageLoader } from "@/components/common/page-loader";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { useSignOut } from "@/features/auth/hooks/use-sign-out";
import { ProfileMediaSection } from "@/features/instructor/components/profile-media-section";
import { RegisterForm } from "@/features/instructor/components/register-form";
import { useInstructorProfile, useUpdateInstructorProfile } from "@/features/instructor/hooks/use-instructor-schedule";
import { profileFormFromInstructor } from "@/features/instructor/schemas";
import type { InstructorProfile } from "@/features/instructor/types";
import { useAreas } from "@/features/marketplace/hooks/use-areas";
import { toUserMessage } from "@/services/http/api-error";

export function InstructorProfilePage() {
  usePageTitle("Instructor profile");
  const profile = useInstructorProfile();
  const areas = useAreas();
  const updateProfile = useUpdateInstructorProfile();

  return (
    <main className="mx-auto max-w-5xl space-y-6 px-4 py-8 sm:px-8">
      <h1 className="font-heading text-2xl font-medium">Profile</h1>
      {profile.isLoading || areas.isLoading ? <PageLoader /> : null}
      {profile.isError ? (
        <ErrorState message={toUserMessage(profile.error)} onRetry={() => void profile.refetch()} />
      ) : null}
      {areas.isError ? <ErrorState message={toUserMessage(areas.error)} onRetry={() => void areas.refetch()} /> : null}
      {profile.data ? <IdentityBar profile={profile.data} /> : null}
      {profile.data && areas.data && areas.data.length > 0 ? (
        <section className="space-y-3">
          <div className="space-y-1">
            <h2 className="font-heading text-lg font-medium">Teaching profile</h2>
            <p className="text-sm leading-relaxed text-brand-muted">
              Update how students see you, the neighbourhoods you teach in, and the modes you offer. New bookings use
              the prices you save here.
            </p>
          </div>
          <RegisterForm
            key={`${profile.data.id}-${profile.data.areaId}-${profile.data.displayName}`}
            areas={areas.data}
            defaultValues={profileFormFromInstructor(profile.data)}
            busy={updateProfile.isPending}
            submitLabel="Save profile"
            busyLabel="Saving…"
            layout="split"
            onSubmit={(input) => {
              updateProfile.mutate(input, {
                onSuccess: () => toast.success("Profile saved."),
                onError: (error) => toast.error(toUserMessage(error)),
              });
            }}
          />
        </section>
      ) : null}
      {profile.data ? <ProfileMediaSection displayName={profile.data.displayName} /> : null}
    </main>
  );
}

function IdentityBar({ profile }: { profile: InstructorProfile }) {
  const { user } = useAuth();
  const signOut = useSignOut();

  return (
    <section
      aria-label="Your account"
      className="flex flex-col gap-4 rounded-3xl border border-brand-border bg-brand-surface p-5 sm:flex-row sm:items-center sm:p-6"
    >
      <div className="flex min-w-0 flex-1 items-center gap-4">
        <Avatar
          sx={{
            width: 56,
            height: 56,
            bgcolor: "primary.main",
            fontSize: 22,
          }}
          aria-hidden="true"
        >
          {profile.displayName.trim().charAt(0).toUpperCase()}
        </Avatar>
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <p className="truncate font-heading text-lg font-medium">{profile.displayName}</p>
            <Chip label={profile.status} size="small" />
          </div>
          <p className="text-sm text-brand-muted">
            {profile.area} · <PhoneText value={user?.phone} />
          </p>
        </div>
      </div>
      <Button
        variant="outlined"
        onClick={signOut}
        startIcon={<LogOut className="size-4" aria-hidden="true" />}
        sx={{ minHeight: 44, borderRadius: "14px", flexShrink: 0 }}
      >
        Sign out
      </Button>
    </section>
  );
}
