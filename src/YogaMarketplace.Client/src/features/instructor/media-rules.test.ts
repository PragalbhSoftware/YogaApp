import { describe, expect, it } from "vitest";
import {
  certificateFileName,
  certificateRules,
  fileProblem,
  formatFileSize,
  introVideoSchema,
  photoRules,
} from "@/features/instructor/media-rules";

function fakeFile(size: number, type: string) {
  const file = new File(["x"], "upload", { type });
  Object.defineProperty(file, "size", { value: size });
  return file;
}

describe("fileProblem", () => {
  it("accepts allowed types within the size limit", () => {
    expect(fileProblem(fakeFile(photoRules.maxBytes, "image/webp"), photoRules)).toBeNull();
    expect(fileProblem(fakeFile(1000, "application/pdf"), certificateRules)).toBeNull();
  });

  it("explains empty, too large and wrong-type files", () => {
    expect(fileProblem(fakeFile(0, "image/png"), photoRules)).toBe("The file is empty.");
    expect(fileProblem(fakeFile(photoRules.maxBytes + 1, "image/png"), photoRules)).toBe("The file must be 5 MB or smaller.");
    expect(fileProblem(fakeFile(1000, "application/pdf"), photoRules)).toBe(photoRules.typeMessage);
    expect(fileProblem(fakeFile(1000, "image/webp"), certificateRules)).toBe(certificateRules.typeMessage);
  });
});

describe("introVideoSchema", () => {
  it("allows YouTube links or an empty value", () => {
    expect(introVideoSchema.safeParse({ url: "https://youtu.be/dQw4w9WgXcQ" }).success).toBe(true);
    expect(introVideoSchema.safeParse({ url: "https://www.youtube.com/watch?v=dQw4w9WgXcQ" }).success).toBe(true);
    expect(introVideoSchema.safeParse({ url: "" }).success).toBe(true);
  });

  it("rejects other sites", () => {
    expect(introVideoSchema.safeParse({ url: "https://vimeo.com/1" }).success).toBe(false);
    expect(introVideoSchema.safeParse({ url: "https://youtube.com.evil.example/x" }).success).toBe(false);
  });
});

it("names downloaded certificates safely", () => {
  expect(certificateFileName("RYT 200", "application/pdf")).toBe("RYT 200.pdf");
  expect(certificateFileName("../../etc/passwd", "image/png")).toBe("....etcpasswd.png");
  expect(certificateFileName("***", "image/jpeg")).toBe("certificate.jpg");
});

it("formats file sizes", () => {
  expect(formatFileSize(512)).toBe("1 KB");
  expect(formatFileSize(2.5 * 1024 * 1024)).toBe("2.5 MB");
});
