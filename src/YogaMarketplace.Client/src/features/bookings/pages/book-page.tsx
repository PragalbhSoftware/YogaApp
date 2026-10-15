import { useCallback, useState } from "react";
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
import { BookingSteps } from "@/features/bookings/components/booking-steps";
import { HomeVisitForm } from "@/features/bookings/components/home-visit-form";
import { PaymentChoice } from "@/features/bookings/components/payment-choice";
import { TrialChoice } from "@/features/bookings/components/trial-choice";
import { useBookCheckout } from "@/features/bookings/hooks/use-book-checkout";
import { useBookingQuote, useTrialStatus } from "@/features/bookings/hooks/use-bookings";
import type { BookingRecord } from "@/features/bookings/types";
import { toHomeVisitFormValues, type HomeVisitFormValues, type HomeVisitValues } from "@/features/bookings/schemas";
import {
  payButtonLabel,
  resolvePayMethod,
  showCreditChoice,
  type PayMethod,
} from "@/features/bookings/utils/pay-method";
import { CreditsPanel } from "@/features/credits/components/credits-panel";
import { PayAgreement } from "@/features/content/components/pay-agreement";
import { useInstructor, useInstructorOpenSlots } from "@/features/marketplace/hooks/use-instructors";
import { areaClosedDescription, areaClosedTitle } from "@/features/marketplace/utils/areas";
import { offeredSessionModes } from "@/features/marketplace/utils/modes";
import { VisitAddressCard } from "@/features/profile/components/visit-address-card";
import { useCustomerProfile, useSaveVisitAddress } from "@/features/profile/hooks/use-profile";
import type { VisitAddress } from "@/features/profile/types";
import { toUserMessage } from "@/services/http/api-error";
import { slotHasEnded, addMonths, endOfMonth, kolkataToday, startOfMonth } from "@/utils/clock";
import { toastError } from "@/services/http/toast-error";
import { emptyBookingDraft, useBookingDraftStore } from "@/stores/booking-draft-store";

function readTrialChoice(value: string | null): boolean | null {
  if (value === "1") return true;
  if (value === "0") return false;
  return null;
}

function readPayChoice(value: string | null): PayMethod | null {
  return value === "credit" || value === "pay" ? value : null;
}

function readMode(value: string | null, allowed: SessionMode[]): SessionMode {
  if (value && allowed.includes(value as SessionMode)) return value as SessionMode;
  if (value && sessionModes.includes(value as SessionMode)) return value as SessionMode;
  return allowed[0] ?? "Home";
}

export function BookPage() {
  usePageTitle("Book a session");
  const navigate = useNavigate();
  const { providerId, slotId } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const instructor = useInstructor(providerId);
  const allowed = instructor.data ? offeredSessionModes(instructor.data.modes) : [...sessionModes];
  const mode = readMode(searchParams.get("mode"), allowed);
  const today = kolkataToday();
  const slotsFrom = startOfMonth(today);
  const slotsTo = endOfMonth(addMonths(today, 2));
  const areaOpen = instructor.data?.areaOpen ?? false;
  const slotsQuery = useInstructorOpenSlots(providerId, mode, slotsFrom, slotsTo, instructor.isSuccess && areaOpen);
  const profile = useCustomerProfile();
  const saveAddress = useSaveVisitAddress();
  const [booking, setBooking] = useState<BookingRecord | null>(null);
  const [checkoutOpen, setCheckoutOpen] = useState(false);
  const draftKey = slotId ?? "";
  const draft = useBookingDraftStore((state) => state.drafts[draftKey]) ?? emptyBookingDraft;
  const saveDraft = useBookingDraftStore((state) => state.saveDraft);
  const clearDraft = useBookingDraftStore((state) => state.clearDraft);
  const changingAddress = draft.changingAddress;
  const setChangingAddress = (changing: boolean) => saveDraft(draftKey, { changingAddress: changing, address: null });
  const saveDraftAddress = useCallback(
    (address: HomeVisitFormValues) => saveDraft(draftKey, { address }),
    [saveDraft, draftKey],
  );

  /** Kept in the URL, replacing the history entry, so a trip to a policy page and back keeps the choice. */
  function setChoice(name: "trial" | "pay", value: string) {
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current);
        next.set(name, value);
        return next;
      },
      { replace: true },
    );
  }

  const slot = (slotsQuery.data?.slots ?? []).find((item) => item.id === slotId);
  const offersTrial = Boolean(instructor.data?.trial) && areaOpen;
  const trialStatus = useTrialStatus(providerId, offersTrial);
  const trialAvailable = offersTrial && trialStatus.data?.offered === true && !trialStatus.data.used;
  const trialChoice = readTrialChoice(searchParams.get("trial"));
  const bookTrial = trialAvailable && (trialChoice ?? true);
  const quote = useBookingQuote(slotId, bookTrial, Boolean(slot) && !booking && !trialStatus.isLoading);
  const payChoice = readPayChoice(searchParams.get("pay"));
  const method = resolvePayMethod(quote.data?.credit, payChoice);
  const savedAddress = profile.data?.visitAddress ?? null;
  const { checkout: pay, pending } = useBookCheckout({
    slotId,
    trial: bookTrial,
    method,
    quote: quote.data,
    onBooked: (booked) => {
      clearDraft(draftKey);
      setBooking(booked);
    },
    onLostCredit: () => {
      setChoice("pay", "pay");
      void quote.refetch();
    },
    onCheckoutOpenChange: setCheckoutOpen,
  });
  const busy = pending || saveAddress.isPending || checkoutOpen;
  const payLabel = payButtonLabel(method, quote.data);
  const busyLabel = method === "credit" && !quote.data?.credit.total ? "Booking…" : "Opening payment…";

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
      toastError(error);
    }
  }

  async function payAfterAddress(home: HomeVisitValues, form: HomeVisitFormValues) {
    try {
      await saveAddress.mutateAsync(form);
      setChangingAddress(false);
      await pay(home);
    } catch (error) {
      toastError(error);
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

  if (instructor.data && !areaOpen) {
    return (
      <main className="mx-auto max-w-lg px-4 py-8">
        <EmptyState
          title={areaClosedTitle}
          description={areaClosedDescription}
          actionLabel="Browse instructors"
          onAction={() => navigate(routes.home)}
        />
      </main>
    );
  }

  if (!instructor.data || !slot) {
    return (
      <main className="mx-auto max-w-lg px-4 py-8">
        <Button
          component={Link}
          to={teacherPath(providerId)}
          variant="text"
          startIcon={<ArrowLeft className="size-4" />}
        >
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

  const payButton = (
    <Button
      variant="contained"
      fullWidth
      disabled={busy}
      onClick={() => void pay()}
      sx={{ minHeight: 48, borderRadius: "14px" }}
    >
      {busy ? busyLabel : payLabel}
    </Button>
  );

  return (
    <main className="mx-auto max-w-5xl space-y-6 px-4 py-6 sm:px-8">
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
        <p className="mt-1 text-sm text-brand-muted">
          Pay now or use a pack credit. The instructor then accepts your request.
        </p>
      </header>

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_22rem] lg:gap-8">
        <aside aria-label="Order summary" className="space-y-4 lg:sticky lg:top-20 lg:order-last">
          <BookSummary
            instructor={instructor.data}
            slot={slot}
            quote={quote.data}
            quoteLoading={quote.isLoading || trialStatus.isLoading}
            withCredit={method === "credit"}
          />
          {quote.isError ? (
            <ErrorState message={toUserMessage(quote.error)} onRetry={() => void quote.refetch()} />
          ) : null}
          {mode === "Home" ? (
            <p className="hidden text-sm text-brand-muted lg:block">Confirm the visit address to pay.</p>
          ) : (
            <div className="hidden lg:block">{payButton}</div>
          )}
          <PayAgreement />
        </aside>

        <div className="space-y-6">
          {trialAvailable && instructor.data.trial ? (
            <TrialChoice
              trialPrice={instructor.data.trial.price}
              useTrial={bookTrial}
              disabled={busy}
              onChange={(useTrial) => setChoice("trial", useTrial ? "1" : "0")}
            />
          ) : null}
          {offersTrial && trialStatus.data?.used ? (
            <p className="text-sm text-brand-muted">
              You’ve already had your trial class with {instructor.data.displayName}.
            </p>
          ) : null}

          {quote.data && showCreditChoice(quote.data.credit) ? (
            <PaymentChoice
              quote={quote.data}
              method={method}
              disabled={busy}
              onChange={(next) => setChoice("pay", next)}
            />
          ) : null}
          {!bookTrial ? <CreditsPanel /> : null}

          {mode === "Home" ? (
            <HomeCheckout
              key={draftKey}
              profileLoading={profile.isLoading}
              profileError={profile.isError}
              profileMessage={profile.error ? toUserMessage(profile.error) : ""}
              onRetryProfile={() => void profile.refetch()}
              savedAddress={savedAddress}
              draftAddress={draft.address}
              onDraftAddress={saveDraftAddress}
              changingAddress={changingAddress}
              busy={busy}
              payLabel={payLabel}
              busyLabel={busyLabel}
              onChangeAddress={() => setChangingAddress(true)}
              onCancelChange={() => setChangingAddress(false)}
              onPayWithSaved={() => void payWithSaved()}
              onSaveChangedAddress={(home, form) => void saveChangedAddress(home, form)}
              onSubmitAddress={(home, form) => void payAfterAddress(home, form)}
            />
          ) : (
            <div className="lg:hidden">{payButton}</div>
          )}

          <BookingSteps instructorName={instructor.data.displayName} />
        </div>
      </div>
    </main>
  );
}

type HomeCheckoutProps = {
  profileLoading: boolean;
  profileError: boolean;
  profileMessage: string;
  onRetryProfile: () => void;
  savedAddress: VisitAddress | null;
  draftAddress: HomeVisitFormValues | null;
  onDraftAddress: (address: HomeVisitFormValues) => void;
  changingAddress: boolean;
  busy: boolean;
  payLabel: string;
  busyLabel: string;
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
  draftAddress,
  onDraftAddress,
  changingAddress,
  busy,
  payLabel,
  busyLabel,
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
            {busy ? busyLabel : payLabel}
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
      defaultValues={draftAddress ?? (savedAddress ? toHomeVisitFormValues(savedAddress) : undefined)}
      onValuesChange={onDraftAddress}
      description={
        savedAddress
          ? "Update the visit address. This becomes your default for the next booking."
          : "Home sessions need a full address so the instructor can reach you. We’ll keep it for next time."
      }
      submitLabel={savedAddress ? "Save address" : payLabel}
      busyLabel={savedAddress ? "Saving…" : busyLabel}
      cancelLabel="Cancel"
      onCancel={savedAddress ? onCancelChange : undefined}
      onSubmit={savedAddress ? onSaveChangedAddress : onSubmitAddress}
    />
  );
}
