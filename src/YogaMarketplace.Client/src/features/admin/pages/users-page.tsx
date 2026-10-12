import { useMemo } from "react";
import { Link } from "react-router-dom";
import { Button, InputAdornment, TextField } from "@mui/material";
import { Search } from "lucide-react";
import { useForm } from "react-hook-form";
import { PhoneText } from "@/components/common/phone-text";
import { adminUserPath } from "@/constants/routes";
import { AdminPage } from "@/features/admin/components/admin-page";
import { AdminTable, RowCard, type AdminColumn } from "@/features/admin/components/admin-table";
import { BlockedBadge } from "@/features/admin/components/blocked-badge";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { PagedList } from "@/features/admin/components/paged-list";
import { StatusPill } from "@/features/admin/components/status-pill";
import { useAdminUsers } from "@/features/admin/hooks/use-admin";
import { ADMIN_PAGE_SIZE, useListParams } from "@/features/admin/hooks/use-list-params";
import { useUserSearchStore } from "@/features/admin/stores/user-search-store";
import { adminRoles, type AdminUserSummary } from "@/features/admin/types";
import { displayName, formatAdminWhen } from "@/features/admin/utils";

const SEARCH_MAX_LENGTH = 100;

const roleFilters = [
  { value: null, label: "Customers and instructors" },
  { value: "Customer", label: "Customers" },
  { value: "Provider", label: "Instructors" },
  { value: "Admin", label: "Admins" },
];

function roleLabel(role: string) {
  return role === "Provider" ? "Instructor" : role;
}

export function UsersPage() {
  const { searchParams, page, update, setPage } = useListParams();
  const roleParam = searchParams.get("role");
  const role = adminRoles.includes(roleParam as (typeof adminRoles)[number]) ? (roleParam ?? undefined) : undefined;
  const term = useUserSearchStore((state) => state.term);
  const setTerm = useUserSearchStore((state) => state.setTerm);
  const users = useAdminUsers({ q: term || undefined, role, page, pageSize: ADMIN_PAGE_SIZE });

  return (
    <AdminPage
      kicker="Directory"
      title="Users"
      lead="Find a customer or instructor by name or phone, then open their account. OTP codes are never shown."
    >
      <section className="flex flex-col gap-4 rounded-[24px] border border-brand-border bg-brand-surface p-4 sm:p-5">
        <UserSearchForm
          initial={term}
          onSearch={(next) => {
            setTerm(next);
            setPage(1);
          }}
        />
        <FilterChips value={role ?? null} options={roleFilters} onChange={(next) => update({ role: next })} label="Role" />
      </section>

      <PagedList
        query={users}
        noun="users"
        empty={{ title: "No users match", description: "Try another name, phone, or role." }}
        onPageChange={setPage}
      >
        {(items, stale) => <UsersTable users={items} stale={stale} />}
      </PagedList>
    </AdminPage>
  );
}

function UserSearchForm({ initial, onSearch }: { initial: string; onSearch: (term: string) => void }) {
  const form = useForm<{ q: string }>({ defaultValues: { q: initial } });

  return (
    <form
      role="search"
      className="flex flex-col gap-3 sm:flex-row"
      onSubmit={form.handleSubmit((values) => onSearch(values.q.trim()))}
    >
      <TextField
        label="Name or phone"
        fullWidth
        autoComplete="off"
        slotProps={{
          htmlInput: { maxLength: SEARCH_MAX_LENGTH },
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <Search className="size-4 text-brand-muted" aria-hidden="true" />
              </InputAdornment>
            ),
          },
        }}
        {...form.register("q")}
      />
      <Button type="submit" variant="contained" sx={{ minHeight: 56, borderRadius: "14px", flexShrink: 0, px: 4 }}>
        Search
      </Button>
    </form>
  );
}

function UsersTable({ users, stale }: { users: AdminUserSummary[]; stale: boolean }) {
  const columns = useMemo<AdminColumn<AdminUserSummary>[]>(
    () => [
      {
        id: "name",
        header: "Name",
        cell: ({ row }) => <UserLink user={row.original} />,
      },
      { id: "role", header: "Role", cell: ({ row }) => roleLabel(row.original.role) },
      { id: "phone", header: "Phone", cell: ({ row }) => <PhoneText value={row.original.phone} /> },
      {
        id: "instructor",
        header: "Instructor profile",
        cell: ({ row }) => <InstructorProfile user={row.original} />,
      },
      {
        id: "joined",
        header: "Joined",
        meta: { className: "whitespace-nowrap text-brand-muted" },
        cell: ({ row }) => formatAdminWhen(row.original.createdAt),
      },
    ],
    [],
  );

  return (
    <AdminTable
      caption="Users"
      data={users}
      columns={columns}
      getRowId={(user) => user.id}
      stale={stale}
      renderCard={(user) => (
        <RowCard title={<UserLink user={user} />} status={user.provider ? <StatusPill status={user.provider.status} /> : null}>
          <p>
            {roleLabel(user.role)} · <PhoneText value={user.phone} />
          </p>
          {user.provider ? (
            <p>
              {user.provider.displayName} · {user.provider.area}
            </p>
          ) : null}
          <p>Joined {formatAdminWhen(user.createdAt)}</p>
        </RowCard>
      )}
    />
  );
}

function UserLink({ user }: { user: AdminUserSummary }) {
  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      <Link className="font-medium text-brand-text hover:text-brand-primary hover:underline" to={adminUserPath(user.id)}>
        {displayName(user.name, user.phone)}
      </Link>
      {user.isBlocked ? <BlockedBadge /> : null}
    </span>
  );
}

function InstructorProfile({ user }: { user: AdminUserSummary }) {
  if (!user.provider) return <span className="text-brand-muted">—</span>;
  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      <span>
        {user.provider.displayName} · {user.provider.area}
      </span>
      <StatusPill status={user.provider.status} />
    </span>
  );
}
