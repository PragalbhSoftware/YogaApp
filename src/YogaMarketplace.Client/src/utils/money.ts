export function formatInrAmount(amount: number) {
  return Math.round(Number(amount)).toLocaleString("en-IN", { maximumFractionDigits: 0 });
}

export function formatInr(amount: number) {
  return `₹${formatInrAmount(amount)}`;
}
