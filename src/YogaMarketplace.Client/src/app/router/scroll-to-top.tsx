import { useEffect, useRef } from "react";
import { NavigationType, useLocation, useNavigationType } from "react-router-dom";

/** New pages start at the top; Back (POP) leaves the position to the browser so the user returns where they were. */
export function ScrollToTop() {
  const { pathname } = useLocation();
  const navigationType = useNavigationType();
  const lastPathname = useRef(pathname);

  useEffect(() => {
    if (lastPathname.current === pathname) return;
    lastPathname.current = pathname;
    if (navigationType !== NavigationType.Pop) window.scrollTo(0, 0);
  }, [pathname, navigationType]);

  return null;
}
