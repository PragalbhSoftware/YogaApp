import { Link } from "react-router-dom";
import { routes } from "@/constants/routes";

const links = [
  { to: routes.refundPolicy, label: "Refund policy" },
  { to: routes.terms, label: "Terms" },
  { to: routes.privacy, label: "Privacy" },
];

export function SiteFooter() {
  return (
    <footer className="border-t border-brand-border px-4 py-6 sm:px-8">
      <div className="mx-auto flex max-w-5xl flex-col gap-3 text-sm text-brand-muted sm:flex-row sm:items-center sm:justify-between">
        <p>Yoga Marketplace · India</p>
        <nav aria-label="Policies" className="flex flex-wrap gap-x-5 gap-y-2">
          {links.map((link) => (
            <Link key={link.to} to={link.to} className="underline-offset-4 hover:text-brand-text hover:underline">
              {link.label}
            </Link>
          ))}
        </nav>
      </div>
    </footer>
  );
}
