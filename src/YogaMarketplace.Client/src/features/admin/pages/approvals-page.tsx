import { Tab, Tabs } from "@mui/material";
import { toast } from "sonner";
import { AdminPage } from "@/features/admin/components/admin-page";
import { CertificateReviewList } from "@/features/admin/components/certificate-review-list";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { PagedList } from "@/features/admin/components/paged-list";
import { ProviderReviewCard } from "@/features/admin/components/provider-review-card";
import { useAdminProviders, useRejectProvider, useVerifyProvider } from "@/features/admin/hooks/use-admin";
import { REVIEW_PAGE_SIZE, useListParams } from "@/features/admin/hooks/use-list-params";
import { certificateStatuses, providerStatuses } from "@/features/admin/types";
import { toUserMessage } from "@/services/http/api-error";

const filters = [
  { value: "Pending", label: "Pending" },
  { value: "Verified", label: "Verified" },
  { value: "Rejected", label: "Rejected" },
  { value: "all", label: "All" },
];

type ApprovalView = "instructors" | "certificates";

function readStatus(value: string | null, allowed: readonly string[]) {
  if (value === "all") return "all";
  return value && allowed.includes(value) ? value : "Pending";
}

export function ApprovalsPage() {
  const { searchParams, page, update, setPage } = useListParams();
  const view: ApprovalView = searchParams.get("view") === "certificates" ? "certificates" : "instructors";
  const status = readStatus(searchParams.get("status"), view === "certificates" ? certificateStatuses : providerStatuses);
  const onStatusChange = (next: string | null) => update({ status: next ?? "Pending" });

  return (
    <AdminPage
      kicker="Instructors"
      title="Approvals"
      lead="Verify a pending instructor so customers can browse them. Approve a certificate to give them the Verified badge."
    >
      <Tabs
        value={view}
        onChange={(_, next: ApprovalView) => update({ view: next, status: null })}
        aria-label="What to review"
        sx={{
          minHeight: 48,
          "& .MuiTab-root": { fontFamily: "inherit", fontWeight: 600, textTransform: "none" },
        }}
      >
        <Tab value="instructors" label="Instructors" />
        <Tab value="certificates" label="Certificates" />
      </Tabs>

      {view === "instructors" ? (
        <InstructorApprovals status={status} page={page} onStatusChange={onStatusChange} onPageChange={setPage} />
      ) : (
        <CertificateReviewList status={status} page={page} onStatusChange={onStatusChange} onPageChange={setPage} />
      )}
    </AdminPage>
  );
}

type InstructorApprovalsProps = {
  status: string;
  page: number;
  onStatusChange: (status: string | null) => void;
  onPageChange: (page: number) => void;
};

function InstructorApprovals({ status, page, onStatusChange, onPageChange }: InstructorApprovalsProps) {
  const providers = useAdminProviders({ status, page, pageSize: REVIEW_PAGE_SIZE });
  const verify = useVerifyProvider();
  const reject = useRejectProvider();
  const busyId =
    (verify.isPending ? verify.variables : undefined) ?? (reject.isPending ? reject.variables?.id : undefined);

  return (
    <>
      <FilterChips value={status} options={filters} onChange={onStatusChange} label="Instructor status" />
      <PagedList
        query={providers}
        noun="instructors"
        empty={{
          title: "No instructors in this list",
          description: "Pending profiles show here after a customer registers as an instructor.",
        }}
        onPageChange={onPageChange}
      >
        {(items, stale) => (
          <div className={stale ? "grid gap-4 opacity-60 lg:grid-cols-2" : "grid gap-4 lg:grid-cols-2"}>
            {items.map((provider) => (
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
        )}
      </PagedList>
    </>
  );
}
