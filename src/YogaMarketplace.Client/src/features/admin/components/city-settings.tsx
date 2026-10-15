import { Button, FormControlLabel, MenuItem, Switch, TextField } from "@mui/material";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { ListPagination } from "@/features/admin/components/list-pagination";
import { SettingsCard } from "@/features/admin/components/settings-card";
import { useAdminCities, useAdminStates, useCreateCity, useUpdateCity } from "@/features/admin/hooks/use-admin";
import { cityEditSchema, citySchema, type CityEditValues, type CityFormValues } from "@/features/admin/schemas";
import type { AdminCity } from "@/features/admin/types";
import { toUserMessage } from "@/services/http/api-error";
import { toastError } from "@/services/http/toast-error";

function readPage(value: string | null) {
  const page = Number(value);
  return Number.isInteger(page) && page > 0 ? page : 1;
}

export function CitySettings() {
  const [searchParams, setSearchParams] = useSearchParams();
  const page = readPage(searchParams.get("page"));
  const states = useAdminStates(1);
  const form = useForm<CityFormValues>({
    resolver: zodResolver(citySchema),
    defaultValues: { stateId: searchParams.get("state") ?? "", name: "", displayOrder: 0 },
  });
  const stateId = form.watch("stateId");
  const cities = useAdminCities(stateId, page);
  const createCity = useCreateCity();
  const selected = states.data?.items.find((state) => state.id === stateId);

  function pickState(id: string) {
    form.setValue("stateId", id, { shouldValidate: form.formState.isSubmitted });
    const params = new URLSearchParams(searchParams);
    if (id) params.set("state", id);
    else params.delete("state");
    params.delete("page");
    setSearchParams(params, { replace: true });
  }

  function setPage(next: number) {
    const params = new URLSearchParams(searchParams);
    params.set("page", String(next));
    setSearchParams(params, { replace: true });
  }

  return (
    <div className="flex flex-col gap-5">
      <SettingsCard className="space-y-0 p-0 sm:p-0">
        <form
          noValidate
          className="flex flex-col gap-3 border-b border-brand-border p-5 sm:p-6"
          onSubmit={form.handleSubmit((values) => {
            createCity.mutate(values, {
              onSuccess: (saved) => {
                toast.success(`${saved.name} added.`);
                form.reset({ stateId: values.stateId, name: "", displayOrder: 0 });
              },
              onError: (error) => toastError(error),
            });
          })}
        >
          <div className="grid gap-3 sm:grid-cols-3">
            <TextField
              select
              label="State"
              fullWidth
              value={stateId}
              onChange={(event) => pickState(event.target.value)}
              error={Boolean(form.formState.errors.stateId)}
              helperText={form.formState.errors.stateId?.message ?? "Filters this list."}
              disabled={states.isLoading || (states.data?.items.length ?? 0) === 0}
            >
              <MenuItem value="">Select</MenuItem>
              {(states.data?.items ?? []).map((state) => (
                <MenuItem key={state.id} value={state.id}>
                  {state.name}
                  {state.isActive ? "" : " (off)"}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              label="City"
              fullWidth
              error={Boolean(form.formState.errors.name)}
              helperText={form.formState.errors.name?.message}
              {...form.register("name")}
              slotProps={{ htmlInput: { maxLength: 80 } }}
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
            disabled={createCity.isPending || selected?.isActive === false}
            sx={{ minHeight: 48, borderRadius: "14px", alignSelf: "flex-start", px: 3 }}
          >
            {createCity.isPending ? "Adding…" : "Add city"}
          </Button>
          {selected?.isActive === false ? (
            <p className="text-sm text-brand-muted">Turn this state on before adding a city.</p>
          ) : null}
        </form>
        {states.isError ? (
          <div className="p-5 sm:p-6">
            <ErrorState message={toUserMessage(states.error)} onRetry={() => void states.refetch()} />
          </div>
        ) : null}
        {!stateId ? (
          <div className="p-5 sm:p-6">
            <EmptyState title="Pick a state" description="Cities are listed for the state you pick." />
          </div>
        ) : null}
        {stateId && cities.isLoading ? (
          <div className="p-5 sm:p-6">
            <BookingListSkeleton />
          </div>
        ) : null}
        {stateId && cities.isError ? (
          <div className="p-5 sm:p-6">
            <ErrorState message={toUserMessage(cities.error)} onRetry={() => void cities.refetch()} />
          </div>
        ) : null}
        {stateId && cities.isSuccess && cities.data.items.length === 0 ? (
          <div className="p-5 sm:p-6">
            <EmptyState title="No cities" description="Add a city for this state." />
          </div>
        ) : null}
        {cities.isSuccess
          ? cities.data.items.map((city) => (
              <section key={city.id} aria-label={city.name} className="border-b border-brand-border p-5 last:border-b-0 sm:px-6">
                <CityRow city={city} />
              </section>
            ))
          : null}
        {cities.isSuccess ? (
          <div className="px-5 pb-5 sm:px-6">
            <ListPagination label="City pages" list={cities.data} onChange={setPage} />
          </div>
        ) : null}
      </SettingsCard>
    </div>
  );
}

function CityRow({ city }: { city: AdminCity }) {
  const updateCity = useUpdateCity();
  const form = useForm<CityEditValues>({
    resolver: zodResolver(cityEditSchema),
    defaultValues: { name: city.name, displayOrder: city.displayOrder, isActive: city.isActive },
  });

  function save(values: CityEditValues, message: string) {
    updateCity.mutate(
      { id: city.id, ...values },
      {
        onSuccess: (saved) => {
          form.reset({ name: saved.name, displayOrder: saved.displayOrder, isActive: saved.isActive });
          toast.success(message);
        },
        onError: (error) => toastError(error),
      },
    );
  }

  return (
    <form className="space-y-3" onSubmit={form.handleSubmit((values) => save(values, "City saved."))}>
      <div className="grid gap-3 sm:grid-cols-2">
        <TextField
          label="City"
          fullWidth
          error={Boolean(form.formState.errors.name)}
          helperText={form.formState.errors.name?.message ?? `Slug ${city.slug}.`}
          {...form.register("name")}
          slotProps={{ htmlInput: { maxLength: 80 } }}
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
                  disabled={updateCity.isPending}
                  onChange={(_, checked) => {
                    field.onChange(checked);
                    void form.trigger().then((valid) => {
                      if (!valid) return;
                      save(
                        { ...form.getValues(), isActive: checked },
                        checked ? "City is on." : "City is off.",
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
          disabled={updateCity.isPending || !form.formState.isDirty}
          sx={{ minHeight: 56, borderRadius: "14px" }}
        >
          {updateCity.isPending ? "Saving…" : "Save"}
        </Button>
      </div>
    </form>
  );
}
