import { CalendarCheck, CreditCard, UserCheck, type LucideIcon } from "lucide-react";

type Step = { icon: LucideIcon; title: string; text: string };

export function BookingSteps({ instructorName }: { instructorName: string }) {
  const steps: Step[] = [
    {
      icon: CreditCard,
      title: "Pay or use a credit",
      text: "Pay securely with Razorpay, or use a session pack credit.",
    },
    { icon: UserCheck, title: "Instructor accepts", text: `${instructorName} confirms your request.` },
    { icon: CalendarCheck, title: "Manage it anytime", text: "Reschedule or cancel from My Bookings." },
  ];

  return (
    <section
      aria-labelledby="booking-steps-heading"
      className="rounded-3xl border border-brand-border bg-brand-surface p-5"
    >
      <h2 id="booking-steps-heading" className="font-heading text-lg font-medium">
        What happens next
      </h2>
      <ol className="mt-4 space-y-4">
        {steps.map((step) => {
          const Icon = step.icon;
          return (
            <li key={step.title} className="flex items-start gap-3">
              <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-brand-primary/10 text-brand-primary">
                <Icon className="size-4" aria-hidden="true" />
              </span>
              <div>
                <p className="text-sm font-medium text-brand-text">{step.title}</p>
                <p className="text-sm text-brand-muted">{step.text}</p>
              </div>
            </li>
          );
        })}
      </ol>
    </section>
  );
}
