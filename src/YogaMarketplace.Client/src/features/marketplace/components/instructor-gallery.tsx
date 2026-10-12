import { apiUrl } from "@/services/http/http-client";

type InstructorGalleryProps = {
  name: string;
  photoUrls: string[];
};

export function InstructorGallery({ name, photoUrls }: InstructorGalleryProps) {
  if (photoUrls.length === 0) return null;
  return (
    <section aria-label={`Photos of ${name}`}>
      <ul className="-mx-4 flex snap-x snap-mandatory gap-3 overflow-x-auto px-4 pb-2 sm:mx-0 sm:grid sm:grid-cols-3 sm:overflow-visible sm:px-0">
        {photoUrls.map((url, index) => (
          <li key={url} className="w-64 shrink-0 snap-start sm:w-auto">
            <img
              src={apiUrl(url)}
              alt={`${name}, photo ${index + 1}`}
              loading={index === 0 ? "eager" : "lazy"}
              className="aspect-[4/3] w-full rounded-3xl object-cover"
            />
          </li>
        ))}
      </ul>
    </section>
  );
}
