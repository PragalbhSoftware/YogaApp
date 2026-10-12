import type { CreditLateCancel, CreditPayBasis } from "@/features/admin/types";

export const payBasisOptions: { value: CreditPayBasis; label: string }[] = [
  { value: "PackPerSession", label: "Pack price ÷ sessions" },
  { value: "SessionPrice", label: "The instructor’s normal session price" },
];

export function payBasisHint(value: CreditPayBasis) {
  const commission = "Commission is taken from this amount, as for paid bookings.";
  return value === "SessionPrice"
    ? `Instructors may earn more per credit session than the customer paid. ${commission}`
    : commission;
}

export const lateCancelOptions: { value: CreditLateCancel; label: string }[] = [
  { value: "PayInstructorShare", label: "Credit used; instructor gets the late-cancel share" },
  { value: "KeepCredit", label: "Credit used; instructor gets nothing" },
  { value: "ReturnCredit", label: "Credit returned" },
];

export function optionLabel<T extends string>(options: { value: T; label: string }[], value: T) {
  return options.find((option) => option.value === value)?.label ?? value;
}
