import { useNavigate } from "react-router-dom";
import { Button, Card, CardContent, Chip, Typography } from "@mui/material";
import { toast } from "sonner";
import { usePageTitle } from "@/hooks/use-page-title";
import { PhoneText } from "@/components/common/phone-text";
import { ErrorState } from "@/components/common/error-state";
import { PageLoader } from "@/components/common/page-loader";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { RegisterForm } from "@/features/instructor/components/register-form";
import {
  useInstructorProfile,
  useUpdateInstructorProfile,
} from "@/features/instructor/hooks/use-instructor-schedule";
import { profileFormFromInstructor } from "@/features/instructor/schemas";
import { useAreas } from "@/features/marketplace/hooks/use-areas";
import { routes } from "@/constants/routes";
import { toUserMessage } from "@/services/http/api-error";

export function InstructorProfilePage() {
  usePageTitle("Instructor profile");
  const { user, signOut } = useAuth();
  const profile = useInstructorProfile();
  const areas = useAreas();
  const updateProfile = useUpdateInstructorProfile();
  const navigate = useNavigate();

  return (
    <main className="mx-auto max-w-lg space-y-6 px-4 py-8 sm:px-8">
      <h1 className="font-heading text-2xl font-medium">Profile</h1>
      {profile.isLoading || areas.isLoading ? <PageLoader /> : null}
      {profile.isError ? (
        <ErrorState message={toUserMessage(profile.error)} onRetry={() => void profile.refetch()} />
      ) : null}
      {areas.isError ? (
        <ErrorState message={toUserMessage(areas.error)} onRetry={() => void areas.refetch()} />
      ) : null}
      {profile.data ? (
        <Card elevation={0} sx={{ borderRadius: 4, border: "1px solid", borderColor: "divider" }}>
          <CardContent className="space-y-3">
            <Chip label={profile.data.status} size="small" />
            <Typography variant="h6">{profile.data.displayName}</Typography>
            <Typography color="text.secondary">{profile.data.area}</Typography>
            <Typography color="text.secondary" component="p">
              <PhoneText value={user?.phone} />
            </Typography>
            <Button
              variant="outlined"
              fullWidth
              onClick={() => {
                signOut();
                navigate(routes.login, { replace: true });
              }}
            >
              Sign out
            </Button>
          </CardContent>
        </Card>
      ) : null}
      {profile.data && areas.data && areas.data.length > 0 ? (
        <section className="space-y-3">
          <div className="space-y-1">
            <h2 className="font-heading text-lg font-medium">Teaching profile</h2>
            <p className="text-sm leading-relaxed text-brand-muted">
              Update how students see you, the neighbourhoods you teach in, and the modes you offer.
              New bookings use the prices you save here.
            </p>
          </div>
          <RegisterForm
            key={`${profile.data.id}-${profile.data.areaId}-${profile.data.displayName}`}
            areas={areas.data}
            defaultValues={profileFormFromInstructor(profile.data)}
            busy={updateProfile.isPending}
            submitLabel="Save profile"
            busyLabel="Saving…"
            onSubmit={(input) => {
              updateProfile.mutate(input, {
                onSuccess: () => toast.success("Profile saved."),
                onError: (error) => toast.error(toUserMessage(error)),
              });
            }}
          />
        </section>
      ) : null}
    </main>
  );
}
