import { Link, useParams } from "react-router-dom";
import { Button } from "@mui/material";
import { ArrowLeft } from "lucide-react";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { PageLoader } from "@/components/common/page-loader";
import { PhoneText } from "@/components/common/phone-text";
import { routes } from "@/constants/routes";
import { AdminPage } from "@/features/admin/components/admin-page";
import { AccountStatusCard } from "@/features/admin/components/account-status-card";
import { ProviderReviewCard } from "@/features/admin/components/provider-review-card";
import {
  useAdminUser,
  useRejectProvider,
  useSetUserBlocked,
  useVerifyProvider,
} from "@/features/admin/hooks/use-admin";
import { displayName, formatAdminWhen } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";
import { toast } from "sonner";

export function UserDetailPage() {
  const { id } = useParams();
  const user = useAdminUser(id);
  const verify = useVerifyProvider();
  const reject = useRejectProvider();
  const setBlocked = useSetUserBlocked();
  const busy =
    (verify.isPending && verify.variables === user.data?.provider?.id) ||
    (reject.isPending && reject.variables?.id === user.data?.provider?.id);

  if (user.isLoading) return <PageLoader />;
  if (user.isError) {
    return (
      <AdminPage kicker="Directory" title="User" lead="Account detail.">
        <ErrorState message={toUserMessage(user.error)} onRetry={() => void user.refetch()} />
      </AdminPage>
    );
  }
  if (!user.data) {
    return (
      <AdminPage kicker="Directory" title="User" lead="Account detail.">
        <EmptyState title="User not found" description="They may have been removed." />
      </AdminPage>
    );
  }

  const account = user.data;

  return (
    <AdminPage
      kicker="Directory"
      title={displayName(account.name, account.phone)}
      lead={`${account.role === "Provider" ? "Instructor" : account.role} account. OTP codes are never shown.`}
    >
      <Button
        component={Link}
        to={routes.adminUsers}
        variant="text"
        startIcon={<ArrowLeft className="size-4" />}
        sx={{ alignSelf: "flex-start", ml: -1 }}
      >
        Back to users
      </Button>

      <dl className="grid gap-3 rounded-[24px] border border-brand-border bg-brand-surface p-5 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-brand-muted">Phone</dt>
          <dd>
            <PhoneText value={account.phone} />
          </dd>
        </div>
        <div>
          <dt className="text-brand-muted">Role</dt>
          <dd>{account.role === "Provider" ? "Instructor" : account.role}</dd>
        </div>
        {account.gender ? (
          <div>
            <dt className="text-brand-muted">Gender</dt>
            <dd>{account.gender}</dd>
          </div>
        ) : null}
        {account.email ? (
          <div>
            <dt className="text-brand-muted">Email</dt>
            <dd>{account.email}</dd>
          </div>
        ) : null}
        <div>
          <dt className="text-brand-muted">Created</dt>
          <dd>{formatAdminWhen(account.createdAt)}</dd>
        </div>
      </dl>

      <AccountStatusCard
        user={account}
        busy={setBlocked.isPending}
        onChange={(blocked, reason) => {
          setBlocked.mutate(
            { id: account.id, blocked, reason },
            {
              onSuccess: () => toast.success(blocked ? "Account blocked." : "Account unblocked."),
              onError: (error) => toast.error(toUserMessage(error)),
            },
          );
        }}
      />

      {account.provider ? (
        <ProviderReviewCard
          provider={account.provider}
          busy={Boolean(busy)}
          onVerify={(providerId) => {
            verify.mutate(providerId, {
              onSuccess: () => {
                toast.success("Verified. Customers can browse this instructor.");
                void user.refetch();
              },
              onError: (error) => toast.error(toUserMessage(error)),
            });
          }}
          onReject={(providerId, reason) => {
            reject.mutate(
              { id: providerId, reason },
              {
                onSuccess: () => {
                  toast.success("Rejected. They stay off public browse.");
                  void user.refetch();
                },
                onError: (error) => toast.error(toUserMessage(error)),
              },
            );
          }}
        />
      ) : null}
    </AdminPage>
  );
}
