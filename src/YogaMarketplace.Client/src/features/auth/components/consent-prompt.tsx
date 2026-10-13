import { Button, Dialog, DialogActions, DialogContent, DialogTitle } from "@mui/material";
import { toast } from "sonner";
import { PolicyLinks } from "@/features/auth/components/policy-links";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { useAcceptConsent } from "@/features/auth/hooks/use-auth-mutations";
import { useSignOut } from "@/features/auth/hooks/use-sign-out";
import { toUserMessage } from "@/services/http/api-error";

/** Asks a signed-in user to accept the Terms and Privacy again after the policy version goes up. */
export function ConsentPrompt() {
  const { user, token, signIn } = useAuth();
  const accept = useAcceptConsent();
  const signOut = useSignOut();

  if (!user?.consentRequired || !token) return null;

  function onAccept() {
    accept.mutate(undefined, {
      onSuccess: (updated) => {
        if (token) signIn(token, updated);
        toast.success("Thanks, you're all set.");
      },
      onError: (err) => toast.error(toUserMessage(err)),
    });
  }

  return (
    <Dialog open aria-labelledby="consent-title" aria-describedby="consent-text">
      <DialogTitle id="consent-title">We've updated our policies</DialogTitle>
      <DialogContent>
        <p id="consent-text" className="text-sm leading-relaxed text-brand-muted">
          Please read and accept the updated <PolicyLinks /> to keep using Yoga Marketplace.
        </p>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={signOut} disabled={accept.isPending}>
          Sign out
        </Button>
        <Button variant="contained" onClick={onAccept} disabled={accept.isPending}>
          {accept.isPending ? "Saving…" : "I accept"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
