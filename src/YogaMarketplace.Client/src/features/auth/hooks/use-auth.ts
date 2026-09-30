import { useAuthStore } from "@/stores/auth-store";

export function useAuth() {
  const user = useAuthStore((state) => state.user);
  const token = useAuthStore((state) => state.token);
  const signIn = useAuthStore((state) => state.signIn);
  const signOut = useAuthStore((state) => state.signOut);
  return { user, token, signIn, signOut, isAuthenticated: Boolean(user && token) };
}
