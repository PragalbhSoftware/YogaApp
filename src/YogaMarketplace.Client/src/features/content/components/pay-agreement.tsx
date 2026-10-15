import { Link } from "react-router-dom";
import { routes } from "@/constants/routes";

const linkClass = "font-medium text-brand-text underline underline-offset-4";

/** Shown next to a pay button. Links stay in the tab; the policy page's Back button returns to the kept checkout. */
export function PayAgreement() {
  return (
    <p className="text-xs leading-relaxed text-brand-muted">
      By paying you agree to the{" "}
      <Link to={routes.terms} className={linkClass}>
        Terms
      </Link>{" "}
      and{" "}
      <Link to={routes.refundPolicy} className={linkClass}>
        Refund policy
      </Link>
      .
    </p>
  );
}
