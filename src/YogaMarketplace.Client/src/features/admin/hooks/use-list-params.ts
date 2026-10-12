import { useSearchParams } from "react-router-dom";

export const ADMIN_PAGE_SIZE = 20;
/** Review cards are tall, so approval lists use smaller pages than the tables. */
export const REVIEW_PAGE_SIZE = 10;

export function readPage(value: string | null) {
  const page = Number(value);
  return Number.isInteger(page) && page > 1 ? page : 1;
}

/**
 * Filters and the page number live in the URL so a list survives refresh and Back.
 * Changing any filter returns to page 1. Never put names, phones or other personal data here.
 */
export function useListParams() {
  const [searchParams, setSearchParams] = useSearchParams();
  const page = readPage(searchParams.get("page"));

  function update(changes: Record<string, string | null>) {
    const params = new URLSearchParams(searchParams);
    for (const [key, value] of Object.entries(changes)) {
      if (value) params.set(key, value);
      else params.delete(key);
    }
    if (!("page" in changes)) params.delete("page");
    setSearchParams(params, { replace: true });
  }

  function setPage(next: number) {
    update({ page: next > 1 ? String(next) : null });
  }

  return { searchParams, page, update, setPage };
}
