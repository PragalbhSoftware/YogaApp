import { ErrorState } from "@/components/common/error-state";
import { PageLoader } from "@/components/common/page-loader";
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
  usePageTitle(page.data?.title ?? "Policy");

  return (
    <main className="mx-auto max-w-2xl px-4 py-8 sm:px-8">
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
