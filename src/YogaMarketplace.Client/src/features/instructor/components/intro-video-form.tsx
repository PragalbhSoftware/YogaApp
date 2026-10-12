import { Button, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { IntroVideo } from "@/features/marketplace/components/intro-video";
import { useSetIntroVideo } from "@/features/instructor/hooks/use-instructor-schedule";
import { introVideoSchema, youTubeWatchUrl, type IntroVideoValues } from "@/features/instructor/media-rules";
import { toUserMessage } from "@/services/http/api-error";

type IntroVideoFormProps = {
  name: string;
  videoId: string | null;
};

export function IntroVideoForm({ name, videoId }: IntroVideoFormProps) {
  const setIntroVideo = useSetIntroVideo();
  const form = useForm<IntroVideoValues>({
    resolver: zodResolver(introVideoSchema),
    values: { url: youTubeWatchUrl(videoId) },
  });

  return (
    <section className="space-y-3">
      <div className="space-y-1">
        <h3 className="font-heading text-base font-medium">Intro video</h3>
        <p className="text-sm text-brand-muted">Paste a YouTube link. Leave it empty and save to remove the video.</p>
      </div>
      <form
        className="flex flex-col gap-3 sm:flex-row sm:items-start"
        noValidate
        onSubmit={form.handleSubmit((values) =>
          setIntroVideo.mutate(values.url, {
            onSuccess: (media) => toast.success(media.introVideoId ? "Intro video saved." : "Intro video removed."),
            onError: (error) => toast.error(toUserMessage(error)),
          }),
        )}
      >
        <TextField
          label="YouTube link"
          placeholder="https://youtu.be/…"
          fullWidth
          error={Boolean(form.formState.errors.url)}
          helperText={form.formState.errors.url?.message}
          slotProps={{ htmlInput: { inputMode: "url", maxLength: 300 } }}
          {...form.register("url")}
        />
        <Button
          type="submit"
          variant="outlined"
          disabled={setIntroVideo.isPending}
          sx={{ minHeight: 56, borderRadius: "14px", flexShrink: 0 }}
        >
          {setIntroVideo.isPending ? "Saving…" : "Save video"}
        </Button>
      </form>
      <IntroVideo name={name} videoId={videoId} />
    </section>
  );
}
