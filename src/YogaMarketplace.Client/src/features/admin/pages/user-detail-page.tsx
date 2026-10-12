import { Link, useParams } from "react-router-dom";
import { Button } from "@mui/material";
import { ArrowLeft } from "lucide-react";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { PageLoader } from "@/components/common/page-loader";
import { PhoneText } from "@/components/common/phone-text";
import { routes } from "@/constants/routes";
import { AccountStatusCard } from "@/features/admin/components/account-status-card";
import { AdminPage } from "@/features/admin/components/admin-page";
import { BlockedBadge } from "@/features/admin/components/blocked-badge";
import { Fact, FactCard } from "@/features/admin/components/fact-card";
import { ProviderReviewCard } from "@/features/admin/components/provider-review-card";
import { UserCreditsCard } from "@/features/admin/components/user-credits-card";
import {
  useAdminUser,
  useRejectProvider,
  useSetUserBlocked,
  useVerifyProvider,
} from "@/features/admin/hooks/use-admin";
import type { AdminProvider, AdminUserDetail } from "@/features/admin/types";
import { displayName, formatAdminWhen } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";

export function UserDetailPage() {
  const { id } = useParams();
  const user = useAdminUser(id);

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

  return <UserDetail account={user.data} onProviderChanged={() => void user.refetch()} />;
}

type UserDetailProps = {
  account: AdminUserDetail;
  onProviderChanged: () => void;
};

function UserDetail({ account, onProviderChanged }: UserDetailProps) {
  const setBlocked = useSetUserBlocked();
  const roleLabel = account.role === "Provider" ? "Instructor" : account.role;

  return (
    <AdminPage
      kicker="Directory"
      title={displayName(account.name, account.phone)}
      lead={`${roleLabel} account. OTP codes are never shown.`}
    >
      <div className="flex flex-wrap items-center gap-3">
        <Button
          component={Link}
          to={routes.adminUsers}
          variant="text"
          startIcon={<ArrowLeft className="size-4" />}
          sx={{ ml: -1 }}
        >
          Back to users
        </Button>
        {account.isBlocked ? <BlockedBadge /> : null}
      </div>

      <div className="grid gap-6 lg:grid-cols-2 lg:items-start">
        <div className="flex flex-col gap-6">
          <FactCard title="Account">
            <Fact label="Phone">
              <PhoneText value={account.phone} />
            </Fact>
            <Fact label="Role">{roleLabel}</Fact>
            {account.gender ? <Fact label="Gender">{account.gender}</Fact> : null}
            {account.email ? <Fact label="Email">{account.email}</Fact> : null}
            <Fact label="Joined">{formatAdminWhen(account.createdAt)}</Fact>
          </FactCard>

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
        </div>

        <div className="flex flex-col gap-6">
          {account.role === "Customer" ? <UserCreditsCard userId={account.id} /> : null}
          {account.provider ? <ProviderSection provider={account.provider} onChanged={onProviderChanged} /> : null}
        </div>
      </div>
    </AdminPage>
  );
}

function ProviderSection({ provider, onChanged }: { provider: AdminProvider; onChanged: () => void }) {
  const verify = useVerifyProvider();
  const reject = useRejectProvider();
  const busy =
    (verify.isPending && verify.variables === provider.id) || (reject.isPending && reject.variables?.id === provider.id);

  return (
    <ProviderReviewCard
      provider={provider}
      busy={busy}
      onVerify={(providerId) => {
        verify.mutate(providerId, {
          onSuccess: () => {
            toast.success("Verified. Customers can browse this instructor.");
            onChanged();
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
              onChanged();
            },
            onError: (error) => toast.error(toUserMessage(error)),
          },
        );
      }}
    />
  );
}
