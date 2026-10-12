import { Sparkles } from "lucide-react";
import type { TrialOffer } from "@/features/marketplace/types";
import { trialPriceLabel } from "@/features/marketplace/utils/trial";

type TrialOfferNoteProps = {
  name: string;
  trial: TrialOffer;
};

export function TrialOfferNote({ name, trial }: TrialOfferNoteProps) {
  return (
    <aside className="flex items-start gap-3 rounded-3xl bg-brand-secondary/15 p-4">
      <Sparkles className="mt-0.5 size-5 shrink-0 text-brand-primary" aria-hidden="true" />
      <div className="space-y-1">
        <p className="font-medium">
          Trial class · {trialPriceLabel(trial.price)}
        </p>
        <p className="text-sm text-brand-muted">
          Your first session with {name}. Pick any time below and choose Trial class when you book.
        </p>
      </div>
    </aside>
  );
}
