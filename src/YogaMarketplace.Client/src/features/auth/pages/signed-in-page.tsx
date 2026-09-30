import { Link } from "react-router-dom";
import { Button, Card, CardContent, Chip, Typography } from "@mui/material";
import { PhoneText } from "@/components/common/phone-text";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { routes } from "@/constants/routes";

const titles: Record<string, string> = {
  Customer: "You are signed in",
  Provider: "Instructor workspace",
  Admin: "Admin area",
};

export function SignedInPage() {
  const { user, signOut } = useAuth();
  const title = titles[user?.role ?? ""] ?? "You are signed in";

  return (
    <main className="mx-auto flex min-h-svh w-full max-w-lg flex-col justify-center px-4 py-10 sm:px-8 md:px-12">
      <Card
        elevation={0}
        sx={{ border: "1px solid", borderColor: "divider", borderRadius: 3, width: "100%" }}
      >
        <CardContent className="space-y-4">
          <Chip label={user?.role} size="small" />
          <Typography variant="h5">{title}</Typography>
          <Typography color="text.secondary" component="p">
            {user?.name ? (
              <>
                {user.name} · <PhoneText value={user.phone} />
              </>
            ) : (
              <PhoneText value={user?.phone} />
            )}
          </Typography>
          <Typography color="text.secondary" variant="body2">
            This workspace is live against the Yoga Marketplace API. Instructor requests and
            admin tools come next in this React app.
          </Typography>
          <Button
            component={Link}
            to={routes.login}
            variant="outlined"
            onClick={signOut}
            fullWidth
            sx={{ maxWidth: { sm: 220 } }}
          >
            Sign out
          </Button>
        </CardContent>
      </Card>
    </main>
  );
}
