import { Button, FormControlLabel, Switch, TextField } from "@mui/material";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { ListPagination } from "@/features/admin/components/list-pagination";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useAdminStates, useCreateState, useImportIndiaLocations, useUpdateState } from "@/features/admin/hooks/use-admin";
import { stateEditSchema, stateSchema, type StateEditValues, type StateFormValues } from "@/features/admin/schemas";
import type { AdminState } from "@/features/admin/types";
import { toUserMessage } from "@/services/http/api-error";
import { toastError } from "@/services/http/toast-error";

function readPage(value: string | null) {
  const page = Number(value);
  return Number.isInteger(page) && page > 0 ? page : 1;
}

export function StateSettings() {
  const [searchParams, setSearchParams] = useSearchParams();
  const page = readPage(searchParams.get("page"));
  const states = useAdminStates(page);

  function setPage(next: number) {
    const params = new URLSearchParams(searchParams);
    params.set("page", String(next));
    setSearchParams(params, { replace: true });
  }

  return (
    <div className="flex flex-col gap-5">
      <SettingsIntro
        title="States"
        lead="Add a state before its cities. A state stays on until every city in it is turned off."
      />
      <SettingsCard className="space-y-0 p-0 sm:p-0">
        <div className="flex flex-col gap-4 border-b border-brand-border p-5 sm:p-6">
          <CreateStateRow />
          <ImportIndiaButton />
        </div>
        {states.isLoading ? (
          <div className="p-5 sm:p-6">
            <BookingListSkeleton />
          </div>
        ) : null}
        {states.isError ? (
          <div className="p-5 sm:p-6">
            <ErrorState message={toUserMessage(states.error)} onRetry={() => void states.refetch()} />
          </div>
        ) : null}
        {states.isSuccess && states.data.items.length === 0 ? (
          <div className="p-5 sm:p-6">
            <EmptyState title="No states" description="Add a state, then add its cities." />
          </div>
        ) : null}
        {states.isSuccess
          ? states.data.items.map((state) => (
              <section key={state.id} aria-label={state.name} className="border-b border-brand-border p-5 last:border-b-0 sm:px-6">
                <StateRow state={state} />
              </section>
            ))
          : null}
        {states.isSuccess ? (
          <div className="px-5 pb-5 sm:px-6">
            <ListPagination label="State pages" list={states.data} onChange={setPage} />
          </div>
        ) : null}
      </SettingsCard>
    </div>
  );
}

function ImportIndiaButton() {
  const importIndia = useImportIndiaLocations();
  return (
    <Button
      type="button"
      variant="outlined"
      disabled={importIndia.isPending}
      onClick={() => {
        importIndia.mutate(undefined, {
          onSuccess: (result) =>
            toast.success(
              `States added ${result.statesAdded}, skipped ${result.statesSkipped}. Cities added ${result.citiesAdded}, skipped ${result.citiesSkipped}.`,
            ),
          onError: (error) => toastError(error),
        });
      }}
      sx={{ minHeight: 48, borderRadius: "14px", alignSelf: "flex-start", px: 3 }}
    >
      {importIndia.isPending ? "Importing…" : "Import India states & cities"}
    </Button>
  );
}

function CreateStateRow() {
  const createState = useCreateState();
  const form = useForm<StateFormValues>({
    resolver: zodResolver(stateSchema),
    defaultValues: { name: "", code: "", displayOrder: 0 },
  });

  return (
    <form
      noValidate
      className="flex flex-col gap-3"
      onSubmit={form.handleSubmit((values) => {
        createState.mutate(values, {
          onSuccess: (saved) => {
            toast.success(`${saved.name} added.`);
            form.reset({ name: "", code: "", displayOrder: 0 });
          },
          onError: (error) => toastError(error),
        });
      })}
    >
      <div className="grid gap-3 sm:grid-cols-3">
        <TextField
          label="Name"
          fullWidth
          error={Boolean(form.formState.errors.name)}
          helperText={form.formState.errors.name?.message}
          {...form.register("name")}
          slotProps={{ htmlInput: { maxLength: 80 } }}
        />
        <TextField
          label="Code"
          fullWidth
          error={Boolean(form.formState.errors.code)}
          helperText={form.formState.errors.code?.message ?? "Letters only. Stored in capitals."}
          {...form.register("code")}
          slotProps={{ htmlInput: { maxLength: 10 } }}
        />
        <TextField
          label="Display order"
          type="number"
          fullWidth
          error={Boolean(form.formState.errors.displayOrder)}
          helperText={form.formState.errors.displayOrder?.message ?? "Lower numbers show first."}
          {...form.register("displayOrder")}
        />
      </div>
      <Button
        type="submit"
        variant="contained"
        disabled={createState.isPending}
        sx={{ minHeight: 48, borderRadius: "14px", alignSelf: "flex-start", px: 3 }}
      >
        {createState.isPending ? "Adding…" : "Add state"}
      </Button>
    </form>
  );
}

function StateRow({ state }: { state: AdminState }) {
  const updateState = useUpdateState();
  const form = useForm<StateEditValues>({
    resolver: zodResolver(stateEditSchema),
    defaultValues: {
      name: state.name,
      code: state.code,
      displayOrder: state.displayOrder,
      isActive: state.isActive,
    },
  });

  function save(values: StateEditValues, message: string) {
    updateState.mutate(
      { id: state.id, ...values },
      {
        onSuccess: (saved) => {
          form.reset({
            name: saved.name,
            code: saved.code,
            displayOrder: saved.displayOrder,
            isActive: saved.isActive,
          });
          toast.success(message);
        },
        onError: (error) => toastError(error),
      },
    );
  }

  return (
    <form className="space-y-3" onSubmit={form.handleSubmit((values) => save(values, "State saved."))}>
      <div className="grid gap-3 sm:grid-cols-3">
        <TextField
          label="Name"
          fullWidth
          error={Boolean(form.formState.errors.name)}
          helperText={form.formState.errors.name?.message}
          {...form.register("name")}
          slotProps={{ htmlInput: { maxLength: 80 } }}
        />
        <TextField
          label="Code"
          fullWidth
          error={Boolean(form.formState.errors.code)}
          helperText={form.formState.errors.code?.message}
          {...form.register("code")}
          slotProps={{ htmlInput: { maxLength: 10 } }}
        />
        <TextField
          label="Display order"
          type="number"
          fullWidth
          error={Boolean(form.formState.errors.displayOrder)}
          helperText={form.formState.errors.displayOrder?.message}
          {...form.register("displayOrder")}
        />
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <Controller
          name="isActive"
          control={form.control}
          render={({ field }) => (
            <FormControlLabel
              sx={{ ml: 0, mr: 0, minHeight: 56 }}
              control={
                <Switch
                  checked={field.value}
                  disabled={updateState.isPending}
                  onChange={(_, checked) => {
                    field.onChange(checked);
                    void form.trigger().then((valid) => {
                      if (!valid) return;
                      save(
                        { ...form.getValues(), isActive: checked },
                        checked ? "State is on." : "State is off.",
                      );
                    });
                  }}
                />
              }
              label={field.value ? "On" : "Off"}
            />
          )}
        />
        <Button
          type="submit"
          variant="outlined"
          disabled={updateState.isPending || !form.formState.isDirty}
          sx={{ minHeight: 56, borderRadius: "14px" }}
        >
          {updateState.isPending ? "Saving…" : "Save"}
        </Button>
      </div>
    </form>
  );
}
