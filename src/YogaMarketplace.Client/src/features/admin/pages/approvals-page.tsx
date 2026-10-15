import { useSearchParams } from "react-router-dom";
import { Tab, Tabs } from "@mui/material";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { AdminPage } from "@/features/admin/components/admin-page";
import { CertificateReviewList } from "@/features/admin/components/certificate-review-list";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { ProviderReviewCard } from "@/features/admin/components/provider-review-card";
import { useAdminProviders, useRejectProvider, useVerifyProvider } from "@/features/admin/hooks/use-admin";
import { certificateStatuses, providerStatuses } from "@/features/admin/types";
import { listCap } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";
import { toastError } from "@/services/http/toast-error";

const filters = [
  { value: "Pending", label: "Pending" },
  { value: "Verified", label: "Verified" },
  { value: "Rejected", label: "Rejected" },
  { value: "all", label: "All" },
];

type ApprovalView = "instructors" | "certificates";

function readStatus(value: string | null, allowed: readonly string[]) {
  if (!value) return "Pending";
  if (value === "all") return "all";
  return allowed.includes(value) ? value : "Pending";
}

function readView(value: string | null): ApprovalView {
  return value === "certificates" ? "certificates" : "instructors";
}

export function ApprovalsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const view = readView(searchParams.get("view"));
  const status = readStatus(
    searchParams.get("status"),
    view === "certificates" ? certificateStatuses : providerStatuses,
  );

  function setParams(next: { view?: ApprovalView; status?: string | null }) {
    const params = new URLSearchParams(searchParams);
    if (next.view) {
      params.set("view", next.view);
      params.delete("status");
    }
    if (next.status !== undefined) params.set("status", next.status ?? "Pending");
    setSearchParams(params, { replace: true });
  }

  return (
    <AdminPage
      kicker="Instructors"
      title="Approvals"
      lead="Verify a pending instructor so customers can browse them. Approve a certificate to give them the Verified badge. Neither creates a booking."
      note={listCap}
    >
      <Tabs
        value={view}
        onChange={(_, next: ApprovalView) => setParams({ view: next })}
        aria-label="What to review"
      >
        <Tab value="instructors" label="Instructors" />
        <Tab value="certificates" label="Certificates" />
      </Tabs>

      {view === "instructors" ? (
        <InstructorApprovals status={status} onStatusChange={(next) => setParams({ status: next })} />
      ) : (
        <CertificateReviewList status={status} onStatusChange={(next) => setParams({ status: next })} />
      )}
    </AdminPage>
  );
}

type InstructorApprovalsProps = {
  status: string;
  onStatusChange: (status: string | null) => void;
};

function InstructorApprovals({ status, onStatusChange }: InstructorApprovalsProps) {
  const providers = useAdminProviders(status);
  const verify = useVerifyProvider();
  const reject = useRejectProvider();
  const busyId =
    (verify.isPending ? verify.variables : undefined) ??
    (reject.isPending ? reject.variables?.id : undefined);

  return (
    <>
      <FilterChips value={status} options={filters} onChange={onStatusChange} label="Instructor status" />

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
                  onError: (error) => toastError(error),
                });
              }}
              onReject={(id, reason) => {
                reject.mutate(
                  { id, reason },
                  {
                    onSuccess: () => toast.success("Rejected. They stay off public browse."),
                    onError: (error) => toastError(error),
                  },
                );
              }}
            />
          ))}
        </div>
      ) : null}
    </>
  );
}
