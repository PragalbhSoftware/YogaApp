import { parsePageBody } from "@/features/content/utils/page-blocks";

type PageBodyProps = {
  body: string;
};

export function PageBody({ body }: PageBodyProps) {
  const blocks = parsePageBody(body);
  return (
    <div className="space-y-4 text-[15px] leading-relaxed text-brand-text">
      {blocks.map((block, index) => {
        const key = `${block.kind}-${index}`;
        if (block.kind === "heading") {
          return (
            <h2 key={key} className="pt-2 font-heading text-xl font-medium">
              {block.text}
            </h2>
          );
        }
        if (block.kind === "list") {
          return (
            <ul key={key} className="list-disc space-y-1.5 pl-5">
              {block.items.map((item, itemIndex) => (
                <li key={`${key}-${itemIndex}`}>{item}</li>
              ))}
            </ul>
          );
        }
        return <p key={key}>{block.text}</p>;
      })}
    </div>
  );
}
