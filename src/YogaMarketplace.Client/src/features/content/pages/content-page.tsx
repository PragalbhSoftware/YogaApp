import { useLocation, useNavigate } from "react-router-dom";
import { Button } from "@mui/material";
import { ArrowLeft } from "lucide-react";
import { ErrorState } from "@/components/common/error-state";
import { PageLoader } from "@/components/common/page-loader";
import { routes } from "@/constants/routes";
import { PageBody } from "@/features/content/components/page-body";
import { useContentPage } from "@/features/content/hooks/use-content-pages";
import type { ContentSlug } from "@/features/content/types";
import { usePageTitle } from "@/hooks/use-page-title";
import { toUserMessage } from "@/services/http/api-error";
import { formatDayKolkata } from "@/utils/clock";

type ContentPageProps = {
  slug: ContentSlug;
};

export function ContentPage({ slug }: ContentPageProps) {
  const page = useContentPage(slug);
  const navigate = useNavigate();
  const location = useLocation();
  usePageTitle(page.data?.title ?? "Policy");

  // "default" is the key of the first page in this tab, so there is nothing in the app to go back to.
  const goBack = () => (location.key === "default" ? navigate(routes.home, { replace: true }) : navigate(-1));

  return (
    <main className="mx-auto max-w-2xl px-4 pt-4 pb-8 sm:px-8">
      <Button
        variant="text"
        onClick={goBack}
        startIcon={<ArrowLeft className="size-5" aria-hidden="true" />}
        sx={{ minHeight: 44, px: 1, mb: 2, ml: -1, fontWeight: 600 }}
      >
        Back
      </Button>
      {page.isLoading ? <PageLoader /> : null}
      {page.isError ? (
        <ErrorState
          title="This page didn’t load"
          message={toUserMessage(page.error)}
          onRetry={() => void page.refetch()}
        />
      ) : null}
      {page.data ? (
        <article className="space-y-6">
          <header className="space-y-1">
            <h1 className="font-heading text-3xl font-medium">{page.data.title}</h1>
            <p className="text-sm text-brand-muted">
              Last updated {formatDayKolkata(page.data.updatedAt)}
            </p>
          </header>
          <PageBody body={page.data.body} />
        </article>
      ) : null}
    </main>
  );
}

export function RefundPolicyPage() {
  return <ContentPage slug="refund-policy" />;
}

export function TermsPage() {
  return <ContentPage slug="terms" />;
}

export function PrivacyPage() {
  return <ContentPage slug="privacy" />;
}
