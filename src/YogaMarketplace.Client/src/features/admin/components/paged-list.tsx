import { useEffect, type ReactNode } from "react";
import type { UseQueryResult } from "@tanstack/react-query";
import { Skeleton } from "@mui/material";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { AdminPager } from "@/features/admin/components/admin-pager";
import { pageCount } from "@/features/admin/list-utils";
import type { Paged } from "@/features/admin/types";
import { toUserMessage } from "@/services/http/api-error";

type PagedListProps<T> = {
  query: UseQueryResult<Paged<T>>;
  noun: string;
  empty: { title: string; description: string };
  onPageChange: (page: number) => void;
  children: (items: T[], stale: boolean) => ReactNode;
};

/** Loading, error, empty and pager for one paged admin list. */
export function PagedList<T>({ query, noun, empty, onPageChange, children }: PagedListProps<T>) {
  const data = query.data;
  const lastPage = data ? pageCount(data.total, data.pageSize) : 1;
  // After the last row on a page is approved or paid, step back instead of showing an empty page.
  const pastTheEnd = Boolean(data && !query.isPlaceholderData && data.items.length === 0 && data.page > lastPage);

  useEffect(() => {
    if (pastTheEnd) onPageChange(lastPage);
  }, [pastTheEnd, lastPage, onPageChange]);

  if (query.isLoading) return <TableSkeleton />;
  if (query.isError && !data) {
    return <ErrorState message={toUserMessage(query.error)} onRetry={() => void query.refetch()} />;
  }
  if (!data || data.total === 0) return <EmptyState title={empty.title} description={empty.description} />;

  return (
    <div className="flex flex-col gap-3">
      {children(data.items, query.isPlaceholderData)}
      <AdminPager total={data.total} page={data.page} pageSize={data.pageSize} noun={noun} onChange={onPageChange} />
    </div>
  );
}

export function TableSkeleton() {
  return (
    <div className="flex flex-col gap-2" aria-hidden="true">
      {Array.from({ length: 6 }, (_, index) => (
        <Skeleton key={index} variant="rounded" height={56} sx={{ borderRadius: "16px" }} />
      ))}
    </div>
  );
}
