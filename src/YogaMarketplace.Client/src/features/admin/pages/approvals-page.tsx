import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { AdminPage } from "@/features/admin/components/admin-page";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { ProviderReviewCard } from "@/features/admin/components/provider-review-card";
import { useAdminProviders, useRejectProvider, useVerifyProvider } from "@/features/admin/hooks/use-admin";
import { providerStatuses } from "@/features/admin/types";
import { listCap } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";

const filters = [
  { value: "Pending", label: "Pending" },
  { value: "Verified", label: "Verified" },
  { value: "Rejected", label: "Rejected" },
  { value: "all", label: "All" },
];

function readStatus(value: string | null) {
  if (!value) return "Pending";
  if (value === "all") return "all";
  return providerStatuses.includes(value as (typeof providerStatuses)[number]) ? value : "Pending";
}

export function ApprovalsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const status = readStatus(searchParams.get("status"));
  const providers = useAdminProviders(status);
  const verify = useVerifyProvider();
  const reject = useRejectProvider();
  const busyId =
    (verify.isPending ? verify.variables : undefined) ??
    (reject.isPending ? reject.variables?.id : undefined);

  function setStatus(next: string | null) {
    const params = new URLSearchParams(searchParams);
    params.set("status", next ?? "Pending");
    setSearchParams(params, { replace: true });
  }

  return (
    <AdminPage
      kicker="Instructors"
      title="Approvals"
      lead="Verify a pending instructor so customers can browse them. Reject keeps them off the public list. This does not create a booking."
      note={listCap}
    >
      <FilterChips value={status} options={filters} onChange={setStatus} label="Instructor status" />

      {providers.isLoading ? <BookingListSkeleton /> : null}
      {providers.isError ? (
        <ErrorState message={toUserMessage(providers.error)} onRetry={() => void providers.refetch()} />
      ) : null}
      {providers.isSuccess && providers.data.length === 0 ? (
        <EmptyState
          title="No instructors in this list"
          description="Pending profiles show here after a customer registers as an instructor."
        />
      ) : null}
      {providers.isSuccess && providers.data.length > 0 ? (
        <div className="space-y-4">
          {providers.data.map((provider) => (
            <ProviderReviewCard
              key={provider.id}
              provider={provider}
              busy={busyId === provider.id}
              onVerify={(id) => {
                verify.mutate(id, {
                  onSuccess: () => toast.success("Verified. Customers can browse this instructor."),
                  onError: (error) => toast.error(toUserMessage(error)),
                });
              }}
              onReject={(id, reason) => {
                reject.mutate(
                  { id, reason },
                  {
                    onSuccess: () => toast.success("Rejected. They stay off public browse."),
                    onError: (error) => toast.error(toUserMessage(error)),
                  },
                );
              }}
            />
          ))}
        </div>
      ) : null}
    </AdminPage>
  );
}
