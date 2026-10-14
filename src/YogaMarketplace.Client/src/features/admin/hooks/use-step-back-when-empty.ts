import { useEffect } from "react";
import type { PagedList } from "@/features/admin/types";

/**
 * When page 2 or later comes back empty (for example its last row was just acknowledged), go back to the last
 * page that still has rows. Only acts on data for the page being shown, so it steps back once, not in a cascade.
 */
export function useStepBackWhenEmpty(list: PagedList<unknown> | undefined, page: number, goTo: (page: number) => void) {
  const emptied = list !== undefined && list.page === page && page > 1 && list.items.length === 0;
  const lastPage = list ? Math.max(1, Math.ceil(list.total / list.pageSize)) : 1;

  useEffect(() => {
    if (emptied) goTo(Math.min(page - 1, lastPage));
  }, [emptied, page, lastPage, goTo]);
}
