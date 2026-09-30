import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { afterSignInPath, routes } from "@/constants/routes";

export function RequireAuth() {
  const { isAuthenticated } = useAuth();
  const location = useLocation();
  if (!isAuthenticated) {
    return <Navigate to={routes.login} replace state={{ from: location }} />;
  }
  return <Outlet />;
}

export function RedirectIfSignedIn() {
  const { user } = useAuth();
  if (user) {
    return <Navigate to={afterSignInPath(user.role)} replace />;
  }
  return <Outlet />;
}

export function RequireRole({ roles }: { roles: string[] }) {
  const { user } = useAuth();
  if (!user || !roles.includes(user.role)) {
    return <Navigate to={afterSignInPath(user?.role)} replace />;
  }
  return <Outlet />;
}
