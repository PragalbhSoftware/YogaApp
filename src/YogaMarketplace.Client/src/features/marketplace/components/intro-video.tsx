import { youTubeEmbedUrl } from "@/features/marketplace/utils/video";

type IntroVideoProps = {
  name: string;
  videoId: string | null;
};

export function IntroVideo({ name, videoId }: IntroVideoProps) {
  const src = youTubeEmbedUrl(videoId);
  if (!src) return null;
  return (
    <section className="space-y-2">
      <h2 className="font-heading text-lg font-medium">Intro video</h2>
      <div className="aspect-video overflow-hidden rounded-3xl bg-brand-primary/5">
        <iframe
          src={src}
          title={`${name} intro video`}
          loading="lazy"
          allow="encrypted-media; picture-in-picture"
          referrerPolicy="strict-origin-when-cross-origin"
          sandbox="allow-scripts allow-same-origin allow-presentation"
          allowFullScreen
          className="size-full border-0"
        />
      </div>
    </section>
  );
}
