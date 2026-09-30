import { Button, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { useAdminCategories, useRenameCategory } from "@/features/admin/hooks/use-admin";
import { renameSchema, type RenameFormValues } from "@/features/admin/schemas";
import type { AdminCategory } from "@/features/admin/types";
import { toUserMessage } from "@/services/http/api-error";

export function CategorySettings() {
  const categories = useAdminCategories();

  if (categories.isLoading) return <BookingListSkeleton />;
  if (categories.isError) {
    return <ErrorState message={toUserMessage(categories.error)} onRetry={() => void categories.refetch()} />;
  }

  return (
    <div className="space-y-5">
      <SettingsIntro
        title="Browse label"
        lead="Customers see this name. The slug stays yoga so public browse does not break."
      />
      {categories.data?.map((category) => (
        <CategoryForm key={category.id} category={category} />
      ))}
    </div>
  );
}

function CategoryForm({ category }: { category: AdminCategory }) {
  const rename = useRenameCategory();
  const form = useForm<RenameFormValues>({
    resolver: zodResolver(renameSchema),
    values: { name: category.name },
  });

  return (
    <SettingsCard>
      <form
        className="space-y-4"
        onSubmit={form.handleSubmit((values) => {
          rename.mutate(
            { id: category.id, name: values.name },
            {
              onSuccess: () => toast.success("Browse label saved."),
              onError: (error) => toast.error(toUserMessage(error)),
            },
          );
        })}
      >
        <TextField
          label="Category name"
          fullWidth
          error={Boolean(form.formState.errors.name)}
          helperText={form.formState.errors.name?.message ?? `Slug stays ${category.slug}.`}
          {...form.register("name")}
          slotProps={{ htmlInput: { maxLength: 80 } }}
        />
        <Button
          type="submit"
          variant="contained"
          disabled={rename.isPending || !form.formState.isDirty}
          sx={{ minHeight: 44, borderRadius: "14px" }}
        >
          {rename.isPending ? "Saving…" : "Save label"}
        </Button>
      </form>
    </SettingsCard>
  );
}
