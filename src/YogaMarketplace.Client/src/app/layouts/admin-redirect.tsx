import { Navigate, useLocation } from "react-router-dom";
import { legacyAdminPath } from "@/app/layouts/admin-redirects";

export function LegacyAdminRedirect() {
  const location = useLocation();
  return <Navigate to={legacyAdminPath(location.pathname, location.search)} replace />;
}
