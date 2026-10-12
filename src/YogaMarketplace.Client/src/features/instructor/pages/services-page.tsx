import { Chip, Skeleton } from "@mui/material";
import { usePageTitle } from "@/hooks/use-page-title";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { TrialSettings } from "@/features/instructor/components/trial-settings";
import { useInstructorServices } from "@/features/instructor/hooks/use-instructor-schedule";
import type { ProviderService } from "@/features/instructor/types";
import { toUserMessage } from "@/services/http/api-error";
import { formatInr } from "@/utils/money";

export function InstructorServicesPage() {
  usePageTitle("Services");
  const services = useInstructorServices();

  return (
    <main className="mx-auto max-w-lg space-y-6 px-4 py-8 sm:px-8">
      <header className="space-y-1">
        <h1 className="font-heading text-2xl font-medium">Services</h1>
        <p className="text-sm leading-relaxed text-brand-muted">
          What you teach. Session prices are set per mode on your profile. Pick which service carries your trial class.
        </p>
      </header>

      {services.isLoading ? (
        <div className="space-y-3" aria-hidden="true">
          <Skeleton variant="rounded" height={72} sx={{ borderRadius: "24px" }} />
          <Skeleton variant="rounded" height={220} sx={{ borderRadius: "24px" }} />
        </div>
      ) : null}
      {services.isError ? (
        <ErrorState message={toUserMessage(services.error)} onRetry={() => void services.refetch()} />
      ) : null}
      {services.isSuccess && services.data.length === 0 ? (
        <EmptyState title="No services yet" description="Your services appear here once your profile is set up." />
      ) : null}
      {services.isSuccess && services.data.length > 0 ? (
        <>
          <ul className="space-y-3">
            {services.data.map((service) => (
              <ServiceRow key={service.id} service={service} />
            ))}
          </ul>
          <TrialSettings services={services.data} />
        </>
      ) : null}
    </main>
  );
}

function ServiceRow({ service }: { service: ProviderService }) {
  return (
    <li className="flex items-center justify-between gap-3 rounded-3xl border border-brand-border bg-brand-surface p-5">
      <div className="min-w-0">
        <p className="font-heading text-base font-medium">{service.title}</p>
        <p className="text-sm text-brand-muted">{service.category}</p>
      </div>
      <div className="flex shrink-0 flex-wrap justify-end gap-2">
        {!service.isActive ? <Chip size="small" label="Inactive" /> : null}
        {service.trialEnabled ? (
          <Chip
            size="small"
            color="primary"
            label={`Trial · ${service.trialPrice ? formatInr(service.trialPrice) : "Free"}`}
          />
        ) : null}
      </div>
    </li>
  );
}
