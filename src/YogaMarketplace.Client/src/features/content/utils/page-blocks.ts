export type PageBlock =
  | { kind: "heading"; text: string }
  | { kind: "paragraph"; text: string }
  | { kind: "list"; items: string[] };

const HEADING = "## ";
const BULLET = "- ";

/**
 * Reads the admin's plain-text page: a blank line ends a paragraph, "## " starts a heading and "- " a bullet.
 * Everything is returned as text, so nothing in the body can inject markup.
 */
export function parsePageBody(body: string): PageBlock[] {
  const blocks: PageBlock[] = [];
  let paragraph: string[] = [];
  let list: string[] = [];

  const flushParagraph = () => {
    if (paragraph.length > 0) blocks.push({ kind: "paragraph", text: paragraph.join(" ") });
    paragraph = [];
  };
  const flushList = () => {
    if (list.length > 0) blocks.push({ kind: "list", items: list });
    list = [];
  };

  for (const raw of body.replace(/\r\n/g, "\n").split("\n")) {
    const line = raw.trim();
    if (!line) {
      flushParagraph();
      flushList();
    } else if (line.startsWith(HEADING)) {
      flushParagraph();
      flushList();
      const text = line.slice(HEADING.length).trim();
      if (text) blocks.push({ kind: "heading", text });
    } else if (line.startsWith(BULLET)) {
      flushParagraph();
      const item = line.slice(BULLET.length).trim();
      if (item) list.push(item);
    } else {
      flushList();
      paragraph.push(line);
    }
  }
  flushParagraph();
  flushList();
  return blocks;
}
