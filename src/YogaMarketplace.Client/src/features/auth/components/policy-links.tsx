import { Link } from "react-router-dom";
import { routes } from "@/constants/routes";

const linkClass = "font-medium text-brand-text underline underline-offset-4";

/** Terms and Privacy links that open in a new tab, so a half-filled form or open dialog is kept. */
export function PolicyLinks() {
  return (
    <>
      <Link to={routes.terms} target="_blank" rel="noopener noreferrer" className={linkClass}>
        Terms of use
      </Link>{" "}
      and{" "}
      <Link to={routes.privacy} target="_blank" rel="noopener noreferrer" className={linkClass}>
        Privacy policy
      </Link>
    </>
  );
}
