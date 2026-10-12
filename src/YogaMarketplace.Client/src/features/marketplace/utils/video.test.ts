import { describe, expect, it } from "vitest";
import { youTubeEmbedUrl } from "@/features/marketplace/utils/video";

describe("youTubeEmbedUrl", () => {
  it("embeds a video id on the privacy-enhanced domain", () => {
    expect(youTubeEmbedUrl("dQw4w9WgXcQ")).toBe("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?rel=0");
  });

  it("refuses anything that is not a video id", () => {
    expect(youTubeEmbedUrl(null)).toBeNull();
    expect(youTubeEmbedUrl("")).toBeNull();
    expect(youTubeEmbedUrl("short")).toBeNull();
    expect(youTubeEmbedUrl('dQw4w9WgX"><')).toBeNull();
    expect(youTubeEmbedUrl("https://youtu.be/dQw4w9WgXcQ")).toBeNull();
  });
});
