import type { ReactNode } from "react";
import { MapPin } from "lucide-react";
import { AddressFacts } from "@/features/profile/components/address-facts";
import type { VisitAddress } from "@/features/profile/types";

type VisitAddressCardProps = {
  address: VisitAddress;
  title?: string;
  description?: string;
  children?: ReactNode;
};

export function VisitAddressCard({
  address,
  title = "Visit address",
  description = "The instructor uses this for Home sessions.",
  children,
}: VisitAddressCardProps) {
  return (
    <section className="flex flex-col gap-5 rounded-3xl border border-brand-border bg-brand-surface p-5 sm:p-6">
      <div className="flex items-start gap-3">
        <span
          className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-brand-primary/10 text-brand-primary"
          aria-hidden="true"
        >
          <MapPin className="size-5" />
        </span>
        <div className="min-w-0">
          <h2 className="font-heading text-lg font-medium">{title}</h2>
          <p className="mt-1 text-sm leading-relaxed text-brand-muted">{description}</p>
        </div>
      </div>
      <AddressFacts
        facts={[
          { label: "House and street", value: address.line1 },
          { label: "Area / locality", value: address.area },
          { label: "City", value: address.city },
          { label: "PIN code", value: address.pin },
          { label: "Landmark", value: address.landmark },
        ]}
      />
      {children}
    </section>
  );
}
