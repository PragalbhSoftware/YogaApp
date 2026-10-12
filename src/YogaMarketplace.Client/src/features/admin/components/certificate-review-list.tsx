import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { adminApi } from "@/features/admin/api/admin-api";
import { CertificateReviewCard } from "@/features/admin/components/certificate-review-card";
import { FilterChips } from "@/features/admin/components/filter-chips";
import {
  useAdminCertificates,
  useApproveCertificate,
  useRejectCertificate,
} from "@/features/admin/hooks/use-admin";
import type { AdminCertificate } from "@/features/admin/types";
import { certificateFileName } from "@/features/instructor/media-rules";
import { toUserMessage } from "@/services/http/api-error";
import { saveBlob } from "@/utils/download";

const filters = [
  { value: "Pending", label: "Pending" },
  { value: "Approved", label: "Approved" },
  { value: "Rejected", label: "Rejected" },
  { value: "all", label: "All" },
];

type CertificateReviewListProps = {
  status: string;
  onStatusChange: (status: string | null) => void;
};

export function CertificateReviewList({ status, onStatusChange }: CertificateReviewListProps) {
  const certificates = useAdminCertificates(status);
  const approve = useApproveCertificate();
  const reject = useRejectCertificate();
  const busyId =
    (approve.isPending ? approve.variables : undefined) ??
    (reject.isPending ? reject.variables?.id : undefined);

  async function open(certificate: AdminCertificate) {
    try {
      saveBlob(
        await adminApi.certificateFile(certificate.id),
        certificateFileName(`${certificate.providerName} ${certificate.title}`, certificate.contentType),
      );
    } catch (error) {
      toast.error(toUserMessage(error));
    }
  }

  return (
    <>
      <FilterChips value={status} options={filters} onChange={onStatusChange} label="Certificate status" />

      {certificates.isLoading ? <BookingListSkeleton /> : null}
      {certificates.isError ? (
        <ErrorState message={toUserMessage(certificates.error)} onRetry={() => void certificates.refetch()} />
      ) : null}
      {certificates.isSuccess && certificates.data.length === 0 ? (
        <EmptyState
          title="No certificates in this list"
          description="Certificates show here after an instructor uploads one from their profile."
        />
      ) : null}
      {certificates.isSuccess && certificates.data.length > 0 ? (
        <div className="space-y-4">
          {certificates.data.map((certificate) => (
            <CertificateReviewCard
              key={certificate.id}
              certificate={certificate}
              busy={busyId === certificate.id}
              onOpen={(item) => void open(item)}
              onApprove={(id) =>
                approve.mutate(id, {
                  onSuccess: () => toast.success("Approved. The instructor now shows as Verified."),
                  onError: (error) => toast.error(toUserMessage(error)),
                })
              }
              onReject={(id, reason) =>
                reject.mutate(
                  { id, reason },
                  {
                    onSuccess: () => toast.success("Rejected. The instructor can upload a new copy."),
                    onError: (error) => toast.error(toUserMessage(error)),
                  },
                )
              }
            />
          ))}
        </div>
      ) : null}
    </>
  );
}
