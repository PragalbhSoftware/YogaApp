import { Link } from "react-router-dom";
import { routes } from "@/constants/routes";

const linkClass = "font-medium text-brand-text underline underline-offset-4";

/** Shown next to a pay button. Links open in a new tab so the checkout is kept. */
export function PayAgreement() {
  return (
    <p className="text-xs leading-relaxed text-brand-muted">
      By paying you agree to the{" "}
      <Link to={routes.terms} target="_blank" rel="noopener noreferrer" className={linkClass}>
        Terms
      </Link>{" "}
      and{" "}
      <Link to={routes.refundPolicy} target="_blank" rel="noopener noreferrer" className={linkClass}>
        Refund policy
      </Link>
      .
    </p>
  );
}
