import { formatInr } from "@/utils/money";
import { cn } from "@/utils/cn";

type PriceTextProps = {
  amount: number;
  className?: string;
};

export function PriceText({ amount, className }: PriceTextProps) {
  return (
    <span
      className={cn(
        "inline-block min-w-[6ch] text-center text-base font-semibold whitespace-nowrap tabular-nums tracking-tight",
        className,
      )}
    >
      {formatInr(amount)}
    </span>
  );
}
