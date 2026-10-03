import { PriceText } from "@/components/common/price-text";
import type { BookingRecord } from "@/features/bookings/types";
import { bookingDetail, joinMeetHref, paymentStatusLabel } from "@/features/bookings/utils/status";
import { AddressFacts } from "@/features/profile/components/address-facts";
import { formatSlotDay, formatTimeRange12 } from "@/utils/clock";

type SessionFactsProps = {
  booking: BookingRecord;
};

export function SessionFacts({ booking }: SessionFactsProps) {
  const meet = joinMeetHref(booking);
  const locationFacts = [
    ...(booking.homeAddress ? [{ label: "Address", value: booking.homeAddress }] : []),
    ...(booking.landmark ? [{ label: "Landmark", value: booking.landmark }] : []),
    ...(booking.studioAddress ? [{ label: "Studio", value: booking.studioAddress }] : []),
  ];

  return (
    <div className="space-y-4">
      <dl className="space-y-2 text-sm">
        <div className="flex justify-between gap-3">
          <dt className="text-brand-muted">When</dt>
          <dd className="text-right">
            {formatSlotDay(booking.date)}
            <span className="mt-0.5 block">
              {formatTimeRange12(booking.start, booking.end)}
            </span>
          </dd>
        </div>
        <div className="flex justify-between gap-3">
          <dt className="text-brand-muted">Session</dt>
          <dd>{booking.mode}</dd>
        </div>
        <div className="flex justify-between gap-3">
          <dt className="text-brand-muted">Fee</dt>
          <dd>
            <PriceText amount={Number(booking.amount)} />
          </dd>
        </div>
        {booking.convenienceFee > 0 ? (
          <div className="flex justify-between gap-3">
            <dt className="text-brand-muted">Convenience fee</dt>
            <dd>
              <PriceText amount={Number(booking.convenienceFee)} />
            </dd>
          </div>
        ) : null}
      </dl>
      {locationFacts.length > 0 ? <AddressFacts facts={locationFacts} /> : null}
      <p className="text-sm">{bookingDetail(booking)}</p>
      {booking.paymentStatus ? (
        <p className="text-xs text-brand-muted">Payment: {paymentStatusLabel(booking.paymentStatus)}</p>
      ) : null}
      {meet ? (
        <a
          href={meet}
          target="_blank"
          rel="noreferrer"
          className="inline-flex min-h-11 items-center text-sm font-medium text-brand-primary"
        >
          Join the session
        </a>
      ) : null}
    </div>
  );
}
