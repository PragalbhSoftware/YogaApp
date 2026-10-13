import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { authApi } from "@/features/auth/api/auth-api";
import { useAuthStore } from "@/stores/auth-store";

export const currentUserKey = ["auth-me"] as const;

/** Refreshes the stored user from the API once per signed-in session, so server-side changes such as a policy bump show up on load. */
export function useCurrentUserSync() {
  const token = useAuthStore((s) => s.token);
  const userId = useAuthStore((s) => s.user?.id);
  const me = useQuery({
    queryKey: [...currentUserKey, userId],
    queryFn: authApi.me,
    enabled: Boolean(token),
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });

  useEffect(() => {
    const { token: current, signIn } = useAuthStore.getState();
    if (me.data && current) signIn(current, me.data);
  }, [me.data]);
}
