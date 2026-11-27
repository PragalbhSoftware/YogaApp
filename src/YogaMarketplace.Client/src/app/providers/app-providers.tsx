import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { CssBaseline, ThemeProvider } from "@mui/material";
import { Toaster } from "sonner";
import { AppRouter, RouteFallback } from "@/app/router/app-router";
import { useClearSessionOnSignOut } from "@/app/providers/use-clear-session-on-sign-out";
import { theme } from "@/app/theme/theme";
import { useRestoredSession } from "@/features/auth/hooks/use-restored-session";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, refetchOnWindowFocus: false },
  },
});

export function AppProviders() {
  useClearSessionOnSignOut(queryClient);
  const restored = useRestoredSession();
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <QueryClientProvider client={queryClient}>
        {restored ? <AppRouter /> : <RouteFallback />}
        <Toaster position="top-center" richColors />
      </QueryClientProvider>
    </ThemeProvider>
  );
}
