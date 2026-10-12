import { toast } from "sonner";
import { adminApi } from "@/features/admin/api/admin-api";
import { CertificateReviewCard } from "@/features/admin/components/certificate-review-card";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { PagedList } from "@/features/admin/components/paged-list";
import {
  useAdminCertificates,
  useApproveCertificate,
  useRejectCertificate,
} from "@/features/admin/hooks/use-admin";
import { REVIEW_PAGE_SIZE } from "@/features/admin/hooks/use-list-params";
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
  page: number;
  onStatusChange: (status: string | null) => void;
  onPageChange: (page: number) => void;
};

export function CertificateReviewList({ status, page, onStatusChange, onPageChange }: CertificateReviewListProps) {
  const certificates = useAdminCertificates({ status, page, pageSize: REVIEW_PAGE_SIZE });
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

      <PagedList
        query={certificates}
        noun="certificates"
        empty={{
          title: "No certificates in this list",
          description: "Certificates show here after an instructor uploads one from their profile.",
        }}
        onPageChange={onPageChange}
      >
        {(items, stale) => (
        <div className={stale ? "grid gap-4 opacity-60 lg:grid-cols-2" : "grid gap-4 lg:grid-cols-2"}>
          {items.map((certificate) => (
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
        )}
      </PagedList>
    </>
  );
}
