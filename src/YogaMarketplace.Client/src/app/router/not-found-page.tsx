import { Button } from "@mui/material";
import { Link } from "react-router-dom";
import { FullPageMessage } from "@/components/common/full-page-message";
import { routes } from "@/constants/routes";
import { usePageTitle } from "@/hooks/use-page-title";

export function NotFoundPage() {
  usePageTitle("Page not found");
  return (
    <FullPageMessage
      kicker="404"
      title="We couldn’t find that page"
      message="The link may be old or mistyped. Head back home to browse instructors."
      actions={
        <Button component={Link} to={routes.home} variant="contained" sx={{ minHeight: 44, borderRadius: "14px" }}>
          Go home
        </Button>
      }
    />
  );
}
