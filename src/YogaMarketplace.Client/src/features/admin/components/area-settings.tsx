import { Button, FormControlLabel, MenuItem, Switch, TextField } from "@mui/material";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard } from "@/features/admin/components/settings-card";
import { useActiveCities, useActiveStates, useAdminAreas, useCreateArea, useUpdateArea } from "@/features/admin/hooks/use-admin";
import { areaEditSchema, areaSchema, type AreaEditValues, type AreaFormValues } from "@/features/admin/schemas";
import type { AdminArea } from "@/features/admin/types";
import { toUserMessage } from "@/services/http/api-error";
import { toastError } from "@/services/http/toast-error";

function groupByCity(areas: AdminArea[]) {
  const groups = new Map<string, AdminArea[]>();
  const sorted = [...areas].sort(
    (a, b) => a.city.localeCompare(b.city, "en") || a.name.localeCompare(b.name, "en"),
  );
  for (const area of sorted) {
    groups.set(area.city, [...(groups.get(area.city) ?? []), area]);
  }
  return [...groups.entries()];
}

export function AreaSettings() {
  const areas = useAdminAreas();

  return (
    <div className="flex flex-col gap-5">
      <SettingsCard className="space-y-0 p-0 sm:p-0">
        <div className="border-b border-brand-border p-5 sm:p-6">
          <CreateAreaRow />
        </div>
        {areas.isLoading ? (
          <div className="p-5 sm:p-6">
            <BookingListSkeleton />
          </div>
        ) : null}
        {areas.isError ? (
          <div className="p-5 sm:p-6">
            <ErrorState message={toUserMessage(areas.error)} onRetry={() => void areas.refetch()} />
          </div>
        ) : null}
        {areas.isSuccess && areas.data.length === 0 ? (
          <div className="p-5 sm:p-6">
            <EmptyState title="No neighbourhoods" description="Add one so customers can pick an area." />
          </div>
        ) : null}
        {areas.isSuccess
          ? groupByCity(areas.data).map(([city, cityAreas]) => (
              <section key={city} aria-label={city} className="border-b border-brand-border last:border-b-0">
                <h3 className="bg-brand-background px-5 py-3 text-[11px] font-semibold tracking-[0.18em] text-brand-muted uppercase sm:px-6">
                  {city} · {cityAreas.length}
                </h3>
                <ul className="divide-y divide-brand-border">
                  {cityAreas.map((area) => (
                    <li key={area.id} className="p-5 sm:px-6">
                      <AreaRow area={area} />
                    </li>
                  ))}
                </ul>
              </section>
            ))
          : null}
      </SettingsCard>
    </div>
  );
}

function CreateAreaRow() {
  const createArea = useCreateArea();
  const states = useActiveStates();
  const form = useForm<AreaFormValues>({
    resolver: zodResolver(areaSchema),
    defaultValues: { name: "", stateId: "", city: "" },
  });
  const stateId = form.watch("stateId");
  const cities = useActiveCities(stateId);

  return (
    <form
      noValidate
      className="flex flex-col gap-3"
      onSubmit={form.handleSubmit((values) => {
        createArea.mutate(
          { name: values.name, city: values.city, stateId: values.stateId },
          {
            onSuccess: (saved) => {
              toast.success(`${saved.name} added in ${saved.city}.`);
              form.reset({ name: "", stateId: values.stateId, city: "" });
            },
            onError: (error) => toastError(error),
          },
        );
      })}
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Controller
          name="stateId"
          control={form.control}
          render={({ field, fieldState }) => (
            <TextField
              select
              label="State"
              fullWidth
              value={field.value}
              onChange={(event) => {
                field.onChange(event.target.value);
                form.setValue("city", "");
              }}
              onBlur={field.onBlur}
              inputRef={field.ref}
              error={Boolean(fieldState.error)}
              helperText={fieldState.error?.message ?? "Required."}
              disabled={states.isLoading || (states.data?.length ?? 0) === 0}
            >
              <MenuItem value="">Select</MenuItem>
              {(states.data ?? []).map((state) => (
                <MenuItem key={state.id} value={state.id}>
                  {state.name}
                </MenuItem>
              ))}
            </TextField>
          )}
        />
        <Controller
          name="city"
          control={form.control}
          render={({ field, fieldState }) => (
            <TextField
              select
              label="City"
              fullWidth
              value={field.value}
              onChange={(event) => field.onChange(event.target.value)}
              onBlur={field.onBlur}
              inputRef={field.ref}
              error={Boolean(fieldState.error)}
              helperText={fieldState.error?.message ?? (stateId ? "Cities in the state you picked." : "Pick a state first.")}
              disabled={!stateId || cities.isLoading}
            >
              <MenuItem value="">Select</MenuItem>
              {(cities.data ?? []).map((city) => (
                <MenuItem key={city.id} value={city.name}>
                  {city.name}
                </MenuItem>
              ))}
            </TextField>
          )}
        />
        <TextField
          label="Neighbourhood"
          fullWidth
          error={Boolean(form.formState.errors.name)}
          helperText={form.formState.errors.name?.message ?? "Shown in the customer picker."}
          {...form.register("name")}
          slotProps={{ htmlInput: { maxLength: 80 } }}
        />
      </div>
      <Button
        type="submit"
        variant="contained"
        disabled={createArea.isPending}
        sx={{ minHeight: 48, borderRadius: "14px", alignSelf: "flex-start", px: 3 }}
      >
        {createArea.isPending ? "Adding…" : "Add neighbourhood"}
      </Button>
    </form>
  );
}

function AreaRow({ area }: { area: AdminArea }) {
  const updateArea = useUpdateArea();
  const form = useForm<AreaEditValues>({
    resolver: zodResolver(areaEditSchema),
    defaultValues: { name: area.name, isActive: area.isActive },
  });

  function save(values: AreaEditValues, message: string) {
    updateArea.mutate(
      { id: area.id, name: values.name, isActive: values.isActive },
      {
        onSuccess: (saved) => {
          form.reset({ name: saved.name, isActive: saved.isActive });
          toast.success(message);
        },
        onError: (error) => toastError(error),
      },
    );
  }

  return (
    <form
      className="space-y-3"
      onSubmit={form.handleSubmit((values) => save(values, "Neighbourhood saved."))}
    >
      <TextField
        label="Neighbourhood"
        fullWidth
        error={Boolean(form.formState.errors.name)}
        helperText={form.formState.errors.name?.message}
        {...form.register("name")}
        slotProps={{ htmlInput: { maxLength: 80 } }}
      />
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
                  disabled={updateArea.isPending}
                  onChange={(_, checked) => {
                    field.onChange(checked);
                    void form.trigger("name").then((valid) => {
                      if (!valid) return;
                      const values = form.getValues();
                      save(
                        { ...values, isActive: checked },
                        checked ? "Open. Instructors here show in browse." : "Hidden from the picker and browse.",
                      );
                    });
                  }}
                />
              }
              label={field.value ? "Open" : "Hidden"}
            />
          )}
        />
        <Button
          type="submit"
          variant="outlined"
          disabled={updateArea.isPending || !form.formState.dirtyFields.name}
          sx={{ minHeight: 56, borderRadius: "14px" }}
        >
          {updateArea.isPending ? "Saving…" : "Save"}
        </Button>
      </div>
    </form>
  );
}
