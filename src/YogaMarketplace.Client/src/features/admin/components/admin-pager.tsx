import { Pagination } from "@mui/material";
import { pageCount, rangeLabel } from "@/features/admin/list-utils";

type AdminPagerProps = {
  total: number;
  page: number;
  pageSize: number;
  noun: string;
  onChange: (page: number) => void;
};

export function AdminPager({ total, page, pageSize, noun, onChange }: AdminPagerProps) {
  const pages = pageCount(total, pageSize);
  return (
    <nav
      aria-label={`${noun} pages`}
      className="flex flex-col items-center justify-between gap-3 pt-2 sm:flex-row"
    >
      <p className="text-sm text-brand-muted" aria-live="polite">
        {rangeLabel(total, page, pageSize, noun)}
      </p>
      {pages > 1 ? (
        <Pagination
          count={pages}
          page={Math.min(page, pages)}
          onChange={(_, next) => onChange(next)}
          shape="rounded"
          color="primary"
          siblingCount={1}
          boundaryCount={1}
        />
      ) : null}
    </nav>
  );
}
