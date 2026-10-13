import { Pagination } from "@mui/material";
import type { PagedList } from "@/features/admin/types";

export function ListPagination({
  label,
  list,
  onChange,
}: {
  label: string;
  list: PagedList<unknown>;
  onChange: (page: number) => void;
}) {
  const pages = Math.ceil(list.total / list.pageSize);
  if (pages <= 1) return null;
  return (
    <nav aria-label={label} className="flex justify-center pt-2">
      <Pagination count={pages} page={list.page} onChange={(_, page) => onChange(page)} shape="rounded" />
    </nav>
  );
}
