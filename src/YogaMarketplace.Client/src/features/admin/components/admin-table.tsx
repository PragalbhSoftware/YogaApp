import type { ReactNode } from "react";
import { useMediaQuery, useTheme } from "@mui/material";
import { tableFeatures, useTable, type ColumnDef, type RowData } from "@tanstack/react-table";
import { cn } from "@/utils/cn";

/** Admin tables only display rows; sorting and paging happen on the server. */
const adminTableFeatures = tableFeatures({
  columnMeta: {} as { className?: string },
});

export type AdminColumn<T extends RowData> = ColumnDef<typeof adminTableFeatures, T>;

type AdminTableProps<T extends RowData> = {
  caption: string;
  data: T[];
  columns: AdminColumn<T>[];
  getRowId: (row: T) => string;
  /** Phones get one card per row instead of a wide table. */
  renderCard: (row: T) => ReactNode;
  /** Dims rows while the next page loads over the current one. */
  stale?: boolean;
};

export function AdminTable<T extends RowData>({ caption, data, columns, getRowId, renderCard, stale }: AdminTableProps<T>) {
  const theme = useTheme();
  const desktop = useMediaQuery(theme.breakpoints.up("md"));
  const table = useTable({
    features: adminTableFeatures,
    data,
    columns,
    getRowId: (row) => getRowId(row),
  });

  if (!desktop) {
    return (
      <ul aria-label={caption} className={cn("flex flex-col gap-3", stale && "opacity-60")}>
        {data.map((row) => (
          <li key={getRowId(row)}>{renderCard(row)}</li>
        ))}
      </ul>
    );
  }

  return (
    <div
      className={cn(
        "overflow-x-auto rounded-[24px] border border-brand-border bg-brand-surface shadow-[0_8px_24px_rgba(37,49,39,0.04)]",
        stale && "opacity-60",
      )}
    >
      <table className="w-full border-collapse text-left text-sm">
        <caption className="sr-only">{caption}</caption>
        <thead>
          {table.getHeaderGroups().map((group) => (
            <tr key={group.id}>
              {group.headers.map((header) => (
                <th
                  key={header.id}
                  scope="col"
                  className={cn(
                    "px-4 py-3 text-[11px] font-medium tracking-[0.14em] whitespace-nowrap text-brand-muted uppercase first:pl-5 last:pr-5",
                    header.column.columnDef.meta?.className,
                  )}
                >
                  {header.isPlaceholder ? null : <table.FlexRender header={header} />}
                </th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody>
          {table.getRowModel().rows.map((row) => (
            <tr key={row.id} className="border-t border-brand-border transition-colors hover:bg-brand-background/60">
              {row.getAllCells().map((cell) => (
                <td
                  key={cell.id}
                  className={cn("px-4 py-3.5 align-middle first:pl-5 last:pr-5", cell.column.columnDef.meta?.className)}
                >
                  <table.FlexRender cell={cell} />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

type RowCardProps = {
  title: ReactNode;
  status?: ReactNode;
  children?: ReactNode;
  action?: ReactNode;
};

/** The phone layout of one table row: title and status on top, facts below, action last. */
export function RowCard({ title, status, children, action }: RowCardProps) {
  return (
    <article className="flex flex-col gap-3 rounded-[24px] border border-brand-border bg-brand-surface p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 font-heading text-base font-medium">{title}</div>
        {status}
      </div>
      {children ? <div className="flex flex-col gap-1 text-sm text-brand-muted">{children}</div> : null}
      {action}
    </article>
  );
}
