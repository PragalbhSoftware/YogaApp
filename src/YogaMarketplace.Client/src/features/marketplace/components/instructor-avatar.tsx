import { apiUrl } from "@/services/http/http-client";
import { initials } from "@/utils/initials";

type InstructorAvatarProps = {
  name: string;
  photoUrl?: string | null;
  size: "md" | "lg";
};

const sizes = {
  md: "size-14 text-lg",
  lg: "size-16 text-xl",
};

export function InstructorAvatar({ name, photoUrl, size }: InstructorAvatarProps) {
  if (photoUrl) {
    return (
      <img
        src={apiUrl(photoUrl)}
        alt=""
        loading="lazy"
        className={`${sizes[size]} shrink-0 rounded-full object-cover`}
      />
    );
  }
  return (
    <div
      className={`${sizes[size]} flex shrink-0 items-center justify-center rounded-full bg-brand-primary/10 font-heading text-brand-primary`}
      aria-hidden="true"
    >
      {initials(name)}
    </div>
  );
}
