import { useState } from "react";
import { Button, MenuItem, TextField } from "@mui/material";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard, SettingsIntro } from "@/features/admin/components/settings-card";
import { PageBody } from "@/features/content/components/page-body";
import { useAdminContentPages, useUpdateContentPage } from "@/features/content/hooks/use-content-pages";
import { contentPageSchema, type ContentPageValues } from "@/features/content/schemas";
import { contentPageLimits, type ContentPage, type ContentSlug } from "@/features/content/types";
import { toUserMessage } from "@/services/http/api-error";

const formatHelp = "Leave a blank line between paragraphs. Start a line with “## ” for a heading or “- ” for a bullet.";

export function PageSettings() {
  const pages = useAdminContentPages();
  const [slug, setSlug] = useState<ContentSlug>("refund-policy");

  if (pages.isLoading) return <BookingListSkeleton />;
  if (pages.isError) {
    return <ErrorState message={toUserMessage(pages.error)} onRetry={() => void pages.refetch()} />;
  }
  if (!pages.data?.length) return null;

  const page = pages.data.find((item) => item.slug === slug) ?? pages.data[0];

  return (
    <div className="space-y-5">
      <SettingsIntro
        title="Policy pages"
        lead="Refund policy, Terms and Privacy are linked in the site footer. Changes go live as soon as you save."
      />
      <TextField
        select
        label="Page"
        value={page.slug}
        onChange={(event) => setSlug(event.target.value as ContentSlug)}
        sx={{ minWidth: 240 }}
      >
        {pages.data.map((item) => (
          <MenuItem key={item.slug} value={item.slug}>
            {item.title}
          </MenuItem>
        ))}
      </TextField>
      <PageForm key={page.slug} page={page} />
    </div>
  );
}

function PageForm({ page }: { page: ContentPage }) {
  const updatePage = useUpdateContentPage();
  const [previewing, setPreviewing] = useState(false);
  const form = useForm<ContentPageValues>({
    resolver: zodResolver(contentPageSchema),
    values: { title: page.title, body: page.body },
  });
  const body = useWatch({ control: form.control, name: "body" });
  const errors = form.formState.errors;

  return (
    <SettingsCard>
      <form
        noValidate
        className="space-y-4"
        onSubmit={form.handleSubmit((values) => {
          updatePage.mutate(
            { slug: page.slug, version: page.version, ...values },
            {
              onSuccess: () => toast.success("Page saved."),
              onError: (error) => toast.error(toUserMessage(error)),
            },
          );
        })}
      >
        <TextField
          label="Title"
          fullWidth
          error={Boolean(errors.title)}
          helperText={errors.title?.message}
          {...form.register("title")}
          slotProps={{ htmlInput: { maxLength: contentPageLimits.titleMax } }}
        />
        {previewing ? (
          <div className="rounded-2xl border border-brand-border bg-brand-background p-4">
            <PageBody body={body} />
          </div>
        ) : (
          <TextField
            label="Text"
            fullWidth
            multiline
            minRows={14}
            error={Boolean(errors.body)}
            helperText={errors.body?.message ?? formatHelp}
            {...form.register("body")}
            slotProps={{ htmlInput: { maxLength: contentPageLimits.bodyMax } }}
          />
        )}
        <div className="flex flex-wrap gap-3">
          <Button
            type="submit"
            variant="contained"
            disabled={updatePage.isPending || !form.formState.isDirty}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {updatePage.isPending ? "Saving…" : "Save page"}
          </Button>
          <Button
            variant="outlined"
            onClick={() => setPreviewing((value) => !value)}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {previewing ? "Edit text" : "Preview"}
          </Button>
        </div>
      </form>
    </SettingsCard>
  );
}
