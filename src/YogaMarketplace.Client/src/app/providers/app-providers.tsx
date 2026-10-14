import { useEffect } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { CssBaseline, ThemeProvider } from "@mui/material";
import { Toaster } from "sonner";
import { AppRouter, RouteFallback } from "@/app/router/app-router";
import { theme } from "@/app/theme/theme";
import { useRestoredSession } from "@/features/auth/hooks/use-restored-session";
import { useAuthStore } from "@/stores/auth-store";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, refetchOnWindowFocus: false },
  },
});

function useClearCacheOnSignOut() {
  useEffect(
    () =>
      useAuthStore.subscribe((state, previous) => {
        if (previous.user && !state.user) queryClient.clear();
      }),
    [],
  );
}

export function AppProviders() {
  useClearCacheOnSignOut();
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
