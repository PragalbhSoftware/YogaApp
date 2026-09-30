import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Button } from "@mui/material";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { AddressCardSkeleton } from "@/components/common/loading-skeleton";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { HomeVisitForm } from "@/features/bookings/components/home-visit-form";
import { toHomeVisitFormValues, type HomeVisitFormValues } from "@/features/bookings/schemas";
import { VisitAddressCard } from "@/features/profile/components/visit-address-card";
import { AccountForm } from "@/features/profile/components/account-form";
import { AccountSummaryCard } from "@/features/profile/components/account-summary-card";
import { useCustomerProfile, useSaveVisitAddress, useUpdateCustomerAccount } from "@/features/profile/hooks/use-profile";
import { routes } from "@/constants/routes";
import { toUserMessage } from "@/services/http/api-error";

export function ProfilePage() {
  const { user, signOut } = useAuth();
  const navigate = useNavigate();
  const profile = useCustomerProfile();
  const saveAddress = useSaveVisitAddress();
  const saveAccount = useUpdateCustomerAccount();
  const [editingAddress, setEditingAddress] = useState(false);
  const savedAddress = profile.data?.visitAddress ?? null;

  async function save(form: HomeVisitFormValues) {
    try {
      await saveAddress.mutateAsync(form);
      setEditingAddress(false);
      toast.success("Visit address saved.");
    } catch (error) {
      toast.error(toUserMessage(error));
    }
  }

  return (
    <main className="mx-auto flex max-w-lg flex-col gap-6 px-4 py-8 sm:px-8">
      <h1 className="font-heading text-2xl font-medium">Profile</h1>
      <AccountSummaryCard
        name={user?.name}
        phone={user?.phone}
        role={user?.role}
        onSignOut={() => {
          signOut();
          navigate(routes.login, { replace: true });
        }}
      />

      {profile.isSuccess ? (
        <AccountForm
          defaultName={profile.data.name ?? user?.name ?? ""}
          defaultGender={profile.data.gender ?? user?.gender ?? null}
          busy={saveAccount.isPending}
          onSubmit={(input) => {
            saveAccount.mutate(input, {
              onSuccess: () => toast.success("Details saved."),
              onError: (error) => toast.error(toUserMessage(error)),
            });
          }}
        />
      ) : null}

      {profile.isLoading ? <AddressCardSkeleton /> : null}
      {profile.isError ? (
        <ErrorState message={toUserMessage(profile.error)} onRetry={() => void profile.refetch()} />
      ) : null}
      {profile.isSuccess && savedAddress && !editingAddress ? (
        <VisitAddressCard
          address={savedAddress}
          title="Home visit address"
          description="Saved for Home sessions so you don’t retype it each time you book."
        >
          <Button variant="outlined" fullWidth onClick={() => setEditingAddress(true)}>
            Edit address
          </Button>
        </VisitAddressCard>
      ) : null}
      {profile.isSuccess && (!savedAddress || editingAddress) ? (
        <HomeVisitForm
          key={savedAddress ? `${savedAddress.line1}|${savedAddress.pin}|${savedAddress.landmark}` : "new"}
          busy={saveAddress.isPending}
          defaultValues={savedAddress ? toHomeVisitFormValues(savedAddress) : undefined}
          title="Home visit address"
          description="Saved for Home sessions so you don’t retype it each time you book."
          submitLabel="Save address"
          busyLabel="Saving…"
          cancelLabel="Cancel"
          onCancel={savedAddress ? () => setEditingAddress(false) : undefined}
          onSubmit={(_, form) => void save(form)}
        />
      ) : null}

      <section className="flex flex-col gap-4 rounded-3xl border border-brand-border bg-brand-surface p-5 sm:p-6">
        <h2 className="font-heading text-lg font-medium">Teach on Yoga Marketplace</h2>
        <p className="-mt-2 text-sm leading-relaxed text-brand-muted">
          Submit your profile for review. Students can browse you only after an admin verifies it.
        </p>
        <Button
          component={Link}
          to={routes.instructorRegister}
          variant="contained"
          fullWidth
          sx={{ minHeight: 44, borderRadius: "14px" }}
        >
          Register as an instructor
        </Button>
      </section>
    </main>
  );
}
