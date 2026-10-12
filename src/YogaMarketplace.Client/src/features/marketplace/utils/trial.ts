import { formatInr } from "@/utils/money";

export function trialPriceLabel(price: number) {
  return price === 0 ? "free" : formatInr(price);
}
