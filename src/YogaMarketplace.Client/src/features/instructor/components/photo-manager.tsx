import { CircularProgress, IconButton } from "@mui/material";
import { ImagePlus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { FileDropZone } from "@/features/instructor/components/file-drop-zone";
import { MediaHeading } from "@/features/instructor/components/media-heading";
import { useAddPhoto, useDeletePhoto } from "@/features/instructor/hooks/use-instructor-schedule";
import { fileProblem, photoRules } from "@/features/instructor/media-rules";
import type { ProviderPhoto } from "@/features/instructor/types";
import { toUserMessage } from "@/services/http/api-error";
import { apiUrl } from "@/services/http/http-client";

type PhotoManagerProps = {
  photos: ProviderPhoto[];
};

export function PhotoManager({ photos }: PhotoManagerProps) {
  const addPhoto = useAddPhoto();
  const deletePhoto = useDeletePhoto();
  const full = photos.length >= photoRules.max;

  function upload(file: File) {
    const problem = fileProblem(file, photoRules);
    if (problem) {
      toast.error(problem);
      return;
    }
    addPhoto.mutate(file, {
      onSuccess: () => toast.success("Photo added."),
      onError: (error) => toast.error(toUserMessage(error)),
    });
  }

  return (
    <section className="space-y-3">
      <MediaHeading
        title="Photos"
        caption="JPEG, PNG or WebP · up to 5 MB each · the first photo is your profile picture"
        count={{ used: photos.length, max: photoRules.max }}
      />
      <ul className="grid grid-cols-3 gap-3">
        {photos.map((photo, index) => (
          <li key={photo.id} className="relative">
            <img
              src={apiUrl(photo.url)}
              alt={`Your photo ${index + 1}`}
              loading="lazy"
              className="aspect-square w-full rounded-2xl object-cover"
            />
            {index === 0 ? (
              <span className="absolute bottom-2 left-2 rounded-full bg-brand-surface/90 px-2 py-0.5 text-[11px] font-medium text-brand-text">
                Profile picture
              </span>
            ) : null}
            <IconButton
              aria-label={`Remove photo ${index + 1}`}
              disabled={deletePhoto.isPending}
              onClick={() =>
                deletePhoto.mutate(photo.id, {
                  onSuccess: () => toast.success("Photo removed."),
                  onError: (error) => toast.error(toUserMessage(error)),
                })
              }
              size="small"
              sx={{
                position: "absolute",
                top: 6,
                right: 6,
                bgcolor: "background.paper",
                "&:hover": { bgcolor: "background.paper" },
              }}
            >
              <Trash2 className="size-4" aria-hidden="true" />
            </IconButton>
          </li>
        ))}
        {!full ? (
          <li>
            <FileDropZone
              label="Add photo"
              accept={photoRules.accept}
              disabled={addPhoto.isPending}
              onPick={upload}
              className="aspect-square w-full p-2"
            >
              {addPhoto.isPending ? (
                <CircularProgress size={22} aria-label="Uploading photo" />
              ) : (
                <ImagePlus className="size-6 text-brand-primary" aria-hidden="true" />
              )}
              <span className="text-xs font-medium text-brand-text">
                {addPhoto.isPending ? "Uploading…" : "Add photo"}
              </span>
              <span className="hidden text-[11px] text-brand-muted sm:block">or drop it here</span>
            </FileDropZone>
          </li>
        ) : null}
      </ul>
    </section>
  );
}
