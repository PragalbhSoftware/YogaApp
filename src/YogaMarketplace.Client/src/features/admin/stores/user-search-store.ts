import { create } from "zustand";

type UserSearchState = {
  term: string;
  setTerm: (term: string) => void;
};

/**
 * The name or phone an admin searched for. Kept in memory only — never in the URL or storage —
 * so it survives opening a user and coming back, and is gone on reload or sign-out.
 */
export const useUserSearchStore = create<UserSearchState>()((set) => ({
  term: "",
  setTerm: (term) => set({ term }),
}));
