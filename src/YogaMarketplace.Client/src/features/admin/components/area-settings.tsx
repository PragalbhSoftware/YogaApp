import { Autocomplete, Button, FormControlLabel, Switch, TextField } from "@mui/material";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useAdminAreas, useCreateArea, useUpdateArea } from "@/features/admin/hooks/use-admin";
import { areaEditSchema, areaSchema, type AreaEditValues, type AreaFormValues } from "@/features/admin/schemas";
import type { AdminArea } from "@/features/admin/types";
import { toUserMessage } from "@/services/http/api-error";

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
  const cities = [...new Set((areas.data ?? []).map((area) => area.city))].sort((a, b) =>
    a.localeCompare(b, "en"),
  );

  return (
    <div className="flex flex-col gap-5">
      <SettingsIntro
        title="Cities and neighbourhoods"
        lead="Customers pick a city, then a neighbourhood. Add neighbourhoods to an open city, or hide one to take it out of browse. Existing bookings there carry on."
      />
      <SettingsCard className="space-y-0 p-0 sm:p-0">
        <div className="border-b border-brand-border p-5 sm:p-6">
          <CreateAreaRow cities={cities} />
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

function CreateAreaRow({ cities }: { cities: string[] }) {
  const createArea = useCreateArea();
  const form = useForm<AreaFormValues>({
    resolver: zodResolver(areaSchema),
    defaultValues: { name: "", city: "" },
  });

  return (
    <form
      noValidate
      className="flex flex-col gap-3"
      onSubmit={form.handleSubmit((values) => {
        createArea.mutate(
          { name: values.name, city: values.city },
          {
            onSuccess: (saved) => {
              toast.success(`${saved.name} added in ${saved.city}.`);
              form.reset({ name: "", city: saved.city });
            },
            onError: (error) => toast.error(toUserMessage(error)),
          },
        );
      })}
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Controller
          name="city"
          control={form.control}
          render={({ field, fieldState }) => (
            <Autocomplete
              options={cities}
              value={field.value || null}
              onChange={(_, next) => field.onChange(next ?? "")}
              disabled={cities.length === 0}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="City"
                  inputRef={field.ref}
                  onBlur={field.onBlur}
                  error={Boolean(fieldState.error)}
                  helperText={fieldState.error?.message ?? "Pick an open city. New cities are not available yet."}
                />
              )}
            />
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
        onError: (error) => toast.error(toUserMessage(error)),
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
