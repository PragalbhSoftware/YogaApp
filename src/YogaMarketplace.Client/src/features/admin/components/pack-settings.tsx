import { Button, Chip, FormControlLabel, Switch, TextField } from "@mui/material";
import { useForm, type UseFormReturn } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useAdminPacks, useCreatePack, useUpdatePack } from "@/features/admin/hooks/use-admin-credits";
import { packSchema, type PackFormValues } from "@/features/admin/schemas";
import type { AdminPack } from "@/features/admin/types";
import { changedFields } from "@/features/admin/utils";
import { ApiError, toUserMessage } from "@/services/http/api-error";
import { formatInr } from "@/utils/money";
import { toastError } from "@/services/http/toast-error";

const emptyPack: PackFormValues = { name: "", sessions: 8, price: 0, validityDays: 60, priceCap: 0 };

function packSaveError(error: unknown) {
  return error instanceof ApiError && error.status === 409
    ? "Another admin changed this pack. We reloaded it; make your edit again."
    : toUserMessage(error);
}

export function PackSettings() {
  const packs = useAdminPacks();

  return (
    <div className="flex flex-col gap-5">
      <SettingsIntro
        title="Session packs"
        lead="Customers buy a pack once and book with credits. Changes apply to new purchases only. Packs can be turned off but never deleted, so past purchases keep their history."
      />
      <SettingsCard>
        <CreatePackForm />
      </SettingsCard>
      {packs.isLoading ? <BookingListSkeleton /> : null}
      {packs.isError ? <ErrorState message={toUserMessage(packs.error)} onRetry={() => void packs.refetch()} /> : null}
      {packs.isSuccess && packs.data.length === 0 ? (
        <EmptyState title="No packs yet" description="Create one above. It shows on the Packs page once it’s on." />
      ) : null}
      {packs.isSuccess
        ? packs.data.map((pack) => (
            <SettingsCard key={`${pack.id}-${pack.version}`}>
              <PackRow pack={pack} />
            </SettingsCard>
          ))
        : null}
    </div>
  );
}

function CreatePackForm() {
  const createPack = useCreatePack();
  const form = useForm<PackFormValues>({ resolver: zodResolver(packSchema), defaultValues: emptyPack });

  return (
    <form
      noValidate
      className="space-y-4"
      onSubmit={form.handleSubmit((values) =>
        createPack.mutate(values, {
          onSuccess: (saved) => {
            toast.success(`${saved.name} created.`);
            form.reset(emptyPack);
          },
          onError: (error) => toastError(error),
        }),
      )}
    >
      <h3 className="font-heading text-base font-medium">New pack</h3>
      <PackFields form={form} />
      <Button
        type="submit"
        variant="contained"
        disabled={createPack.isPending}
        sx={{ minHeight: 44, borderRadius: "14px" }}
      >
        {createPack.isPending ? "Creating…" : "Create pack"}
      </Button>
    </form>
  );
}

function PackRow({ pack }: { pack: AdminPack }) {
  const updatePack = useUpdatePack();
  const form = useForm<PackFormValues>({
    resolver: zodResolver(packSchema),
    defaultValues: {
      name: pack.name,
      sessions: pack.sessions,
      price: pack.price,
      validityDays: pack.validityDays,
      priceCap: pack.priceCap,
    },
  });

  function save(changes: Partial<PackFormValues & { isActive: boolean }>, message: string) {
    updatePack.mutate(
      { id: pack.id, version: pack.version, ...changes },
      { onSuccess: () => toast.success(message), onError: (error) => toastError(error, packSaveError(error)) },
    );
  }

  return (
    <form
      noValidate
      className="space-y-4"
      onSubmit={form.handleSubmit((values) =>
        save(changedFields(values, form.formState.dirtyFields), "Pack saved. New purchases use it."),
      )}
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="font-heading text-base font-medium">{pack.name}</h3>
          <Chip size="small" label={`${pack.sold} sold`} />
          <Chip size="small" label={`${formatInr(pack.price / pack.sessions)} a session`} />
        </div>
        <FormControlLabel
          sx={{ mr: 0 }}
          control={
            <Switch
              checked={pack.isActive}
              disabled={updatePack.isPending}
              onChange={(_, checked) =>
                save({ isActive: checked }, checked ? "Pack is on the Packs page." : "Pack turned off. Bought credits still work.")
              }
            />
          }
          label={pack.isActive ? "On sale" : "Off"}
        />
      </div>
      <PackFields form={form} />
      <Button
        type="submit"
        variant="outlined"
        disabled={updatePack.isPending || !form.formState.isDirty}
        sx={{ minHeight: 44, borderRadius: "14px" }}
      >
        {updatePack.isPending ? "Saving…" : "Save changes"}
      </Button>
    </form>
  );
}

function PackFields({ form }: { form: UseFormReturn<PackFormValues> }) {
  const errors = form.formState.errors;
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <TextField
        label="Name"
        fullWidth
        error={Boolean(errors.name)}
        helperText={errors.name?.message ?? "Shown to customers, for example “8 sessions”."}
        slotProps={{ htmlInput: { maxLength: 80 } }}
        {...form.register("name")}
      />
      <TextField
        label="Sessions"
        fullWidth
        inputMode="numeric"
        error={Boolean(errors.sessions)}
        helperText={errors.sessions?.message ?? "Credits in the pack."}
        {...form.register("sessions")}
      />
      <TextField
        label="Price (₹)"
        fullWidth
        inputMode="decimal"
        error={Boolean(errors.price)}
        helperText={errors.price?.message ?? "What the customer pays once."}
        {...form.register("price")}
      />
      <TextField
        label="Valid for (days)"
        fullWidth
        inputMode="numeric"
        error={Boolean(errors.validityDays)}
        helperText={errors.validityDays?.message ?? "Credits expire at midnight India time after the last day."}
        {...form.register("validityDays")}
      />
      <TextField
        label="Session price cap (₹)"
        fullWidth
        inputMode="decimal"
        error={Boolean(errors.priceCap)}
        helperText={errors.priceCap?.message ?? "A credit covers sessions up to this price. Above it, customers pay."}
        {...form.register("priceCap")}
      />
    </div>
  );
}
