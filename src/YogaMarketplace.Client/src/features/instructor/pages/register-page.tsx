import { Link, useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { PageLoader } from "@/components/common/page-loader";
import { routes } from "@/constants/routes";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { RegisterForm } from "@/features/instructor/components/register-form";
import { emptyInstructorRegisterForm } from "@/features/instructor/schemas";
import { useRegisterInstructor } from "@/features/instructor/hooks/use-instructor-schedule";
import { useAreas } from "@/features/marketplace/hooks/use-areas";
import { toUserMessage } from "@/services/http/api-error";
import { useAreaStore } from "@/stores/area-store";

export function InstructorRegisterPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const areas = useAreas();
  const selectedCity = useAreaStore((state) => state.city);
  const selectedArea = useAreaStore((state) => state.areaName);
  const register = useRegisterInstructor();
  const defaultAreaId =
    areas.data?.find((area) => area.city === selectedCity && area.name === selectedArea)?.id ?? "";

  async function onSubmit(input: Parameters<typeof register.mutateAsync>[0]) {
    try {
      await register.mutateAsync(input);
      toast.success("Submitted for review. You can add slots after an admin verifies your profile.");
      navigate(routes.instructor, { replace: true });
    } catch (error) {
      toast.error(toUserMessage(error));
    }
  }

  return (
    <main className="mx-auto max-w-lg space-y-6 px-4 py-8 sm:px-8">
      <header className="space-y-1">
        <p className="text-[11px] font-medium tracking-[0.22em] text-brand-muted uppercase">Instructor</p>
        <h1 className="font-heading text-2xl font-medium">Register as an instructor</h1>
        <p className="text-sm leading-relaxed text-brand-muted">
          Submit your profile for review. Customers can browse you only after an admin verifies it.
        </p>
      </header>

      {areas.isLoading ? <PageLoader /> : null}
      {areas.isError ? (
        <ErrorState message={toUserMessage(areas.error)} onRetry={() => void areas.refetch()} />
      ) : null}
      {areas.isSuccess && areas.data.length === 0 ? (
        <EmptyState
          title="No neighbourhoods yet"
          description="Areas will appear here once the marketplace lists them."
        />
      ) : null}
      {areas.isSuccess && areas.data.length > 0 ? (
        <RegisterForm
          key={`${user?.id ?? "anon"}-${defaultAreaId}`}
          areas={areas.data}
          defaultValues={{
            ...emptyInstructorRegisterForm(user?.name ?? ""),
            areaId: defaultAreaId,
          }}
          busy={register.isPending}
          onSubmit={(input) => void onSubmit(input)}
        />
      ) : null}

      <p className="text-center text-sm">
        <Link to={routes.profile} className="font-medium text-brand-primary">
          Back to profile
        </Link>
      </p>
    </main>
  );
}
