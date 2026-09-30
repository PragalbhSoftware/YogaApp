import { Link, useSearchParams } from "react-router-dom";
import { Button, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { PhoneText } from "@/components/common/phone-text";
import { adminUserPath } from "@/constants/routes";
import { AdminPage } from "@/features/admin/components/admin-page";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { useAdminUsers } from "@/features/admin/hooks/use-admin";
import { adminRoles } from "@/features/admin/types";
import { displayName, listCap } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";

const roleFilters = [
  { value: null, label: "Any role" },
  { value: "Customer", label: "Customer" },
  { value: "Provider", label: "Instructor" },
  { value: "Admin", label: "Admin" },
];

export function UsersPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const q = searchParams.get("q")?.trim() || undefined;
  const roleParam = searchParams.get("role");
  const role = adminRoles.includes(roleParam as (typeof adminRoles)[number]) ? roleParam ?? undefined : undefined;
  const users = useAdminUsers({ q, role });
  const form = useForm<{ q: string }>({
    defaultValues: { q: q ?? "" },
  });

  function setRole(next: string | null) {
    const params = new URLSearchParams(searchParams);
    if (next) params.set("role", next);
    else params.delete("role");
    setSearchParams(params, { replace: true });
  }

  return (
    <AdminPage
      kicker="Directory"
      title="Users"
      lead="Search by name or phone. Open a row for the full account. OTP codes are never shown here."
      note={listCap}
    >
      <form
        className="flex flex-col gap-3 sm:flex-row"
        onSubmit={form.handleSubmit((values) => {
          const params = new URLSearchParams(searchParams);
          const next = values.q.trim();
          if (next) params.set("q", next);
          else params.delete("q");
          setSearchParams(params, { replace: true });
        })}
      >
        <TextField
          label="Name or phone"
          fullWidth
          autoComplete="off"
          slotProps={{ htmlInput: { maxLength: 80 } }}
          {...form.register("q")}
        />
        <Button type="submit" variant="contained" sx={{ minHeight: 56, borderRadius: "14px", flexShrink: 0 }}>
          Search
        </Button>
      </form>

      <FilterChips value={role ?? null} options={roleFilters} onChange={setRole} label="Role" />

      {users.isLoading ? <BookingListSkeleton /> : null}
      {users.isError ? (
        <ErrorState message={toUserMessage(users.error)} onRetry={() => void users.refetch()} />
      ) : null}
      {users.isSuccess && users.data.length === 0 ? (
        <EmptyState title="No users match" description="Try another name, phone, or role." />
      ) : null}
      {users.isSuccess && users.data.length > 0 ? (
        <div className="space-y-3">
          {users.data.map((user) => (
            <article
              key={user.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-[24px] border border-brand-border bg-brand-surface p-5"
            >
              <div className="min-w-0">
                <h2 className="font-heading text-lg font-medium">{displayName(user.name, user.phone)}</h2>
                <p className="mt-1 text-sm text-brand-muted">
                  {user.role === "Provider" ? "Instructor" : user.role} · <PhoneText value={user.phone} />
                </p>
                {user.provider ? (
                  <p className="mt-1 text-sm text-brand-muted">
                    {user.provider.displayName} · {user.provider.status} · {user.provider.area}
                  </p>
                ) : null}
              </div>
              <Button
                component={Link}
                to={adminUserPath(user.id)}
                variant="outlined"
                sx={{ minHeight: 44, borderRadius: "14px" }}
              >
                View
              </Button>
            </article>
          ))}
        </div>
      ) : null}
    </AdminPage>
  );
}
