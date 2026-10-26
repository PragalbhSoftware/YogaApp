import { useEffect } from "react";
import { useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { Skeleton } from "@mui/material";
import { usePageTitle } from "@/hooks/use-page-title";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { packPath, routes } from "@/constants/routes";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { PayAgreement } from "@/features/content/components/pay-agreement";
import { CreditsPanel } from "@/features/credits/components/credits-panel";
import { PackCard } from "@/features/credits/components/pack-card";
import { PurchaseStatus } from "@/features/credits/components/purchase-status";
import { useBuyPack } from "@/features/credits/hooks/use-buy-pack";
import { usePacks } from "@/features/credits/hooks/use-credits";
import type { Pack } from "@/features/credits/types";
import { toUserMessage } from "@/services/http/api-error";

export function PacksPage() {
  usePageTitle("Session packs");
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const { isAuthenticated } = useAuth();
  const packs = usePacks();
  const purchase = useBuyPack();
  const highlightedId = searchParams.get("pack");

  useEffect(() => {
    if (!highlightedId || !packs.isSuccess) return;
    document.getElementById(`pack-${highlightedId}`)?.scrollIntoView({ block: "center" });
  }, [highlightedId, packs.isSuccess]);

  function onBuy(pack: Pack) {
    if (!isAuthenticated) {
      navigate(routes.login, { state: { from: { ...location, search: `?pack=${encodeURIComponent(pack.id)}` } } });
      return;
    }
    if (highlightedId !== pack.id) navigate(packPath(pack.id), { replace: true });
    void purchase.buy(pack);
  }

  return (
    <main className="mx-auto max-w-5xl space-y-6 px-4 py-8 sm:px-8">
      <header className="max-w-2xl space-y-1">
        <p className="text-[11px] font-medium tracking-[0.22em] text-brand-muted uppercase">Packs</p>
        <h1 className="font-heading text-3xl font-medium">Practise more, pay less</h1>
        <p className="text-sm leading-relaxed text-brand-muted">
          Buy a pack of credits once. Each credit books one session with any instructor, at home, in a studio or online.
          Credits expire at midnight market time after the last day shown.
        </p>
      </header>

      <PurchaseStatus
        stage={purchase.stage}
        batch={purchase.batch}
        onFindInstructor={() => navigate(routes.home)}
        onDismiss={purchase.reset}
        onRetry={purchase.retry}
      />

      {isAuthenticated && purchase.stage === "idle" ? <CreditsPanel /> : null}

      {packs.isLoading ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" aria-hidden="true">
          {Array.from({ length: 3 }, (_, index) => (
            <Skeleton key={index} variant="rounded" height={280} sx={{ borderRadius: "24px" }} />
          ))}
        </div>
      ) : null}
      {packs.isError ? <ErrorState message={toUserMessage(packs.error)} onRetry={() => void packs.refetch()} /> : null}
      {packs.isSuccess && packs.data.length === 0 ? (
        <EmptyState
          title="No packs right now"
          description="Check back soon. You can still book single sessions."
          actionLabel="Find instructors"
          onAction={() => navigate(routes.home)}
        />
      ) : null}
      {packs.isSuccess && packs.data.length > 0 ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {packs.data.map((pack) => (
            <PackCard
              key={pack.id}
              pack={pack}
              highlighted={pack.id === highlightedId}
              busy={purchase.busy}
              buying={purchase.buyingId === pack.id}
              onBuy={onBuy}
            />
          ))}
        </div>
      ) : null}
      {packs.isSuccess && packs.data.length > 0 ? <PayAgreement /> : null}
    </main>
  );
}
