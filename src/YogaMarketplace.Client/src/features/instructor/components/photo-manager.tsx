import { IconButton } from "@mui/material";
import { Trash2 } from "lucide-react";
import { toast } from "sonner";
import { FilePickButton } from "@/features/instructor/components/file-pick-button";
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
      <div className="space-y-1">
        <h3 className="font-heading text-base font-medium">Photos</h3>
        <p className="text-sm text-brand-muted">
          Up to {photoRules.max} photos, JPEG, PNG or WebP, 5 MB each. The first photo is your profile picture.
        </p>
      </div>
      {photos.length > 0 ? (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {photos.map((photo, index) => (
            <li key={photo.id} className="relative">
              <img
                src={apiUrl(photo.url)}
                alt={`Your photo ${index + 1}`}
                loading="lazy"
                className="aspect-square w-full rounded-2xl object-cover"
              />
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
                sx={{ position: "absolute", top: 8, right: 8, bgcolor: "background.paper", "&:hover": { bgcolor: "background.paper" } }}
              >
                <Trash2 className="size-4" aria-hidden="true" />
              </IconButton>
            </li>
          ))}
        </ul>
      ) : null}
      <div className="flex flex-wrap items-center gap-3">
        <FilePickButton
          label={addPhoto.isPending ? "Uploading…" : "Add photo"}
          accept={photoRules.accept}
          disabled={full || addPhoto.isPending}
          onPick={upload}
        />
        <span className="text-sm text-brand-muted">
          {photos.length} of {photoRules.max}
        </span>
      </div>
    </section>
  );
}
