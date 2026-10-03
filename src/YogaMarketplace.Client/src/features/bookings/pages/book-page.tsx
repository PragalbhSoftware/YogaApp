import { useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Button } from "@mui/material";
import { ArrowLeft } from "lucide-react";
import { toast } from "sonner";
import { usePageTitle } from "@/hooks/use-page-title";
import { sessionModes, type SessionMode } from "@/constants/catalog";
import { routes, teacherPath } from "@/constants/routes";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { AddressCardSkeleton } from "@/components/common/loading-skeleton";
import { PageLoader } from "@/components/common/page-loader";
import { BookSummary } from "@/features/bookings/components/book-summary";
import { HomeVisitForm } from "@/features/bookings/components/home-visit-form";
import { useConfirmLocalPayment, useConfirmPayment, useCreateBookingOrder } from "@/features/bookings/hooks/use-bookings";
import {
  openRazorpayCheckout,
  RazorpayCheckoutCancelled,
  RazorpayCheckoutFailed,
} from "@/features/bookings/lib/razorpay-checkout";
import { isFakeOrder, type BookingRecord } from "@/features/bookings/types";
import { toHomeVisitFormValues, type HomeVisitFormValues, type HomeVisitValues } from "@/features/bookings/schemas";
import { digitsOnly } from "@/features/auth/utils/phone";
import { useInstructor, useInstructorOpenSlots } from "@/features/marketplace/hooks/use-instructors";
import { usePolicy } from "@/features/marketplace/hooks/use-site";
import { offeredSessionModes } from "@/features/marketplace/utils/modes";
import { VisitAddressCard } from "@/features/profile/components/visit-address-card";
import { useCustomerProfile, useSaveVisitAddress } from "@/features/profile/hooks/use-profile";
import type { VisitAddress } from "@/features/profile/types";
import { toUserMessage } from "@/services/http/api-error";
import { useAuthStore } from "@/stores/auth-store";
import { slotHasEnded, addMonths, endOfMonth, kolkataToday, startOfMonth } from "@/utils/clock";

function readMode(value: string | null, allowed: SessionMode[]): SessionMode {
  if (value && allowed.includes(value as SessionMode)) return value as SessionMode;
  if (value && sessionModes.includes(value as SessionMode)) return value as SessionMode;
  return allowed[0] ?? "Home";
}

export function BookPage() {
  usePageTitle("Book a session");
  const navigate = useNavigate();
  const { providerId, slotId } = useParams();
  const [searchParams] = useSearchParams();
  const instructor = useInstructor(providerId);
  const allowed = instructor.data ? offeredSessionModes(instructor.data.modes) : [...sessionModes];
  const mode = readMode(searchParams.get("mode"), allowed);
  const today = kolkataToday();
  const slotsFrom = startOfMonth(today);
  const slotsTo = endOfMonth(addMonths(today, 2));
  const slotsQuery = useInstructorOpenSlots(providerId, mode, slotsFrom, slotsTo, instructor.isSuccess);
  const policy = usePolicy();
  const profile = useCustomerProfile();
  const saveAddress = useSaveVisitAddress();
  const createOrder = useCreateBookingOrder();
  const confirmLocal = useConfirmLocalPayment();
  const confirmPayment = useConfirmPayment();
  const [booking, setBooking] = useState<BookingRecord | null>(null);
  const [checkoutOpen, setCheckoutOpen] = useState(false);
  const [changingAddress, setChangingAddress] = useState(false);
  const user = useAuthStore((state) => state.user);
  const busy =
    createOrder.isPending ||
    confirmLocal.isPending ||
    confirmPayment.isPending ||
    saveAddress.isPending ||
    checkoutOpen;

  const slot = (slotsQuery.data?.slots ?? []).find((item) => item.id === slotId);
  const rate = instructor.data?.modes?.find((item) => item.mode === mode)?.rate ?? 0;
  const savedAddress = profile.data?.visitAddress ?? null;

  async function pay(home?: HomeVisitValues) {
    if (!slotId) return;
    try {
      const order = await createOrder.mutateAsync({
        slotId,
        homeAddress: home?.homeAddress,
        landmark: home?.landmark,
      });

      if (order.localCapture || isFakeOrder(order.orderId)) {
        const booked = await confirmLocal.mutateAsync(order.orderId);
        setBooking(booked);
        toast.success("Booked. The instructor will accept your request.");
        return;
      }

      setCheckoutOpen(true);
      const paid = await openRazorpayCheckout({
        keyId: order.keyId,
        orderId: order.orderId,
        amountPaise: order.amountPaise,
        currency: order.currency,
        description: `${order.mode} session with ${order.providerName}`,
        customerName: user?.name,
        customerPhone: digitsOnly(user?.phone ?? ""),
      });
      const booked = await confirmPayment.mutateAsync({
        orderId: paid.orderId,
        paymentId: paid.paymentId,
        signature: paid.signature,
      });
      setBooking(booked);
      toast.success("Booked. The instructor will accept your request.");
    } catch (error) {
      if (error instanceof RazorpayCheckoutCancelled) {
        toast.message(error.message);
        return;
      }
      if (error instanceof RazorpayCheckoutFailed) {
        toast.error(error.message);
        return;
      }
      toast.error(toUserMessage(error));
    } finally {
      setCheckoutOpen(false);
    }
  }

  async function payWithSaved() {
    if (!savedAddress) return;
    await pay({ homeAddress: savedAddress.homeAddress, landmark: savedAddress.landmark });
  }

  async function saveChangedAddress(_home: HomeVisitValues, form: HomeVisitFormValues) {
    try {
      await saveAddress.mutateAsync(form);
      setChangingAddress(false);
      toast.success("Visit address updated.");
    } catch (error) {
      toast.error(toUserMessage(error));
    }
  }

  async function payAfterAddress(home: HomeVisitValues, form: HomeVisitFormValues) {
    try {
      await saveAddress.mutateAsync(form);
      setChangingAddress(false);
      await pay(home);
    } catch (error) {
      toast.error(toUserMessage(error));
    }
  }

  if (!providerId || !slotId) {
    return (
      <main className="mx-auto max-w-lg px-4 py-8">
        <EmptyState title="Missing session" description="Go back and pick a time." />
      </main>
    );
  }

  if (booking) {
    return (
      <main className="mx-auto max-w-lg px-4 py-8">
        <EmptyState
          title="You’re booked"
          description={`${booking.providerName} has your ${booking.mode.toLowerCase()} request. Find it under Bookings.`}
          actionLabel="View bookings"
          onAction={() => navigate(routes.bookings)}
        />
      </main>
    );
  }

  if (instructor.isLoading || slotsQuery.isLoading) return <PageLoader />;

  if (instructor.isError) {
    return (
      <main className="mx-auto max-w-lg px-4 py-8">
        <ErrorState message={toUserMessage(instructor.error)} onRetry={() => void instructor.refetch()} />
      </main>
    );
  }

  if (!instructor.data || !slot) {
    return (
      <main className="mx-auto max-w-lg px-4 py-8">
        <Button component={Link} to={teacherPath(providerId)} variant="text" startIcon={<ArrowLeft className="size-4" />}>
          Back to instructor
        </Button>
        <div className="mt-6">
          <EmptyState
            title="That time is no longer available"
            description="It may have ended, been booked, or been blocked. Pick another slot."
          />
        </div>
      </main>
    );
  }

  if (slotHasEnded(slot.date, slot.end)) {
    return (
      <main className="mx-auto max-w-lg px-4 py-8">
        <EmptyState title="That slot has ended" description="Choose a later time on the instructor’s calendar." />
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-lg space-y-6 px-4 py-6 sm:px-8">
      <Button
        component={Link}
        to={`${teacherPath(providerId)}?mode=${mode}`}
        variant="text"
        startIcon={<ArrowLeft className="size-4" />}
        sx={{ px: 0 }}
      >
        Back to instructor
      </Button>

      <header>
        <p className="text-[11px] font-medium tracking-[0.22em] text-brand-muted uppercase">Book</p>
        <h1 className="font-heading text-2xl font-medium">Book this session</h1>
        <p className="mt-1 text-sm text-brand-muted">Pay now with Razorpay. The instructor then accepts your request.</p>
      </header>

      <BookSummary
        instructor={instructor.data}
        slot={slot}
        rate={Number(rate)}
        convenienceFee={policy.data?.convenienceFee}
        feeLoading={policy.isLoading}
      />

      {mode === "Home" ? (
        <HomeCheckout
          profileLoading={profile.isLoading}
          profileError={profile.isError}
          profileMessage={profile.error ? toUserMessage(profile.error) : ""}
          onRetryProfile={() => void profile.refetch()}
          savedAddress={savedAddress}
          changingAddress={changingAddress}
          busy={busy}
          onChangeAddress={() => setChangingAddress(true)}
          onCancelChange={() => setChangingAddress(false)}
          onPayWithSaved={() => void payWithSaved()}
          onSaveChangedAddress={(home, form) => void saveChangedAddress(home, form)}
          onSubmitAddress={(home, form) => void payAfterAddress(home, form)}
        />
      ) : (
        <Button
          variant="contained"
          fullWidth
          disabled={busy}
          onClick={() => void pay()}
          sx={{ minHeight: 48, borderRadius: "14px" }}
        >
          {busy ? "Opening payment…" : "Pay with Razorpay"}
        </Button>
      )}
    </main>
  );
}

type HomeCheckoutProps = {
  profileLoading: boolean;
  profileError: boolean;
  profileMessage: string;
  onRetryProfile: () => void;
  savedAddress: VisitAddress | null;
  changingAddress: boolean;
  busy: boolean;
  onChangeAddress: () => void;
  onCancelChange: () => void;
  onPayWithSaved: () => void;
  onSaveChangedAddress: (home: HomeVisitValues, form: HomeVisitFormValues) => void;
  onSubmitAddress: (home: HomeVisitValues, form: HomeVisitFormValues) => void;
};

function HomeCheckout({
  profileLoading,
  profileError,
  profileMessage,
  onRetryProfile,
  savedAddress,
  changingAddress,
  busy,
  onChangeAddress,
  onCancelChange,
  onPayWithSaved,
  onSaveChangedAddress,
  onSubmitAddress,
}: HomeCheckoutProps) {
  if (profileLoading) return <AddressCardSkeleton />;
  if (profileError) return <ErrorState message={profileMessage} onRetry={onRetryProfile} />;

  if (savedAddress && !changingAddress) {
    return (
      <VisitAddressCard address={savedAddress}>
        <div className="space-y-2">
          <Button
            variant="contained"
            fullWidth
            disabled={busy}
            onClick={onPayWithSaved}
            sx={{ minHeight: 48, borderRadius: "14px" }}
          >
            {busy ? "Opening payment…" : "Pay with Razorpay"}
          </Button>
          <Button type="button" variant="text" fullWidth disabled={busy} onClick={onChangeAddress}>
            Change address
          </Button>
        </div>
      </VisitAddressCard>
    );
  }

  return (
    <HomeVisitForm
      key={savedAddress ? `${savedAddress.line1}|${savedAddress.pin}|${savedAddress.landmark}` : "new"}
      busy={busy}
      defaultValues={savedAddress ? toHomeVisitFormValues(savedAddress) : undefined}
      description={
        savedAddress
          ? "Update the visit address. This becomes your default for the next booking."
          : "Home sessions need a full address so the instructor can reach you. We’ll keep it for next time."
      }
      submitLabel={savedAddress ? "Save address" : "Pay with Razorpay"}
      busyLabel={savedAddress ? "Saving…" : "Opening payment…"}
      cancelLabel="Cancel"
      onCancel={savedAddress ? onCancelChange : undefined}
      onSubmit={savedAddress ? onSaveChangedAddress : onSubmitAddress}
    />
  );
}
