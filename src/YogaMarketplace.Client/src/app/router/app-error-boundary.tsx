import { Component, type ErrorInfo, type ReactNode } from "react";
import { Button } from "@mui/material";
import { useLocation } from "react-router-dom";
import { FullPageMessage } from "@/components/common/full-page-message";
import { routes } from "@/constants/routes";

type AppErrorBoundaryProps = {
  children: ReactNode;
};

export function AppErrorBoundary({ children }: AppErrorBoundaryProps) {
  const { pathname } = useLocation();
  return <RouteErrorBoundary resetKey={pathname}>{children}</RouteErrorBoundary>;
}

type RouteErrorBoundaryProps = AppErrorBoundaryProps & {
  resetKey: string;
};

type RouteErrorBoundaryState = {
  failed: boolean;
  resetKey: string;
};

class RouteErrorBoundary extends Component<RouteErrorBoundaryProps, RouteErrorBoundaryState> {
  constructor(props: RouteErrorBoundaryProps) {
    super(props);
    this.state = { failed: false, resetKey: props.resetKey };
  }

  static getDerivedStateFromError(): Partial<RouteErrorBoundaryState> {
    return { failed: true };
  }

  static getDerivedStateFromProps(
    props: RouteErrorBoundaryProps,
    state: RouteErrorBoundaryState,
  ): RouteErrorBoundaryState | null {
    return props.resetKey === state.resetKey ? null : { failed: false, resetKey: props.resetKey };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("Unhandled UI error", error, info.componentStack);
  }

  render() {
    if (!this.state.failed) return this.props.children;

    return (
      <FullPageMessage
        role="alert"
        kicker="Something went wrong"
        title="This page hit a problem"
        message="Your bookings and payments are safe. Try loading the page again, or go back home."
        actions={
          <>
            {/* Full page loads also recover from a stale code chunk after a new release. */}
            <Button variant="contained" onClick={() => window.location.reload()} sx={{ minHeight: 44, borderRadius: "14px" }}>
              Try again
            </Button>
            <Button href={routes.home} variant="outlined" sx={{ minHeight: 44, borderRadius: "14px" }}>
              Go home
            </Button>
          </>
        }
      />
    );
  }
}
