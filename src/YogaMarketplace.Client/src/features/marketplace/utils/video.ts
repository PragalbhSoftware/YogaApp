const YOUTUBE_ID = /^[A-Za-z0-9_-]{11}$/;

/** Privacy-enhanced embed URL, or null if the id is not a YouTube video id. */
export function youTubeEmbedUrl(videoId: string | null | undefined) {
  if (!videoId || !YOUTUBE_ID.test(videoId)) return null;
  return `https://www.youtube-nocookie.com/embed/${videoId}?rel=0`;
}
