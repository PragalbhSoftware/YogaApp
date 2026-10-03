import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it } from "vitest";
import { RedirectIfSignedIn, RequireAuth, RequireRole } from "@/app/router/guards";
import { routes } from "@/constants/routes";
import { useAuthStore } from "@/stores/auth-store";

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<RedirectIfSignedIn />}>
          <Route path={routes.login} element={<p>login page</p>} />
        </Route>
        <Route element={<RequireAuth />}>
          <Route element={<RequireRole roles={["Customer"]} />}>
            <Route path={routes.home} element={<p>customer home</p>} />
          </Route>
          <Route element={<RequireRole roles={["Provider"]} />}>
            <Route path={routes.instructor} element={<p>instructor schedule</p>} />
          </Route>
          <Route element={<RequireRole roles={["Admin"]} />}>
            <Route path={routes.admin} element={<p>admin dashboard</p>} />
          </Route>
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

function signInAs(role: string) {
  useAuthStore.getState().signIn("token", { id: "u1", name: "Test", phone: "+919000000000", gender: null, role });
}

describe("route guards", () => {
  beforeEach(() => useAuthStore.getState().signOut());

  it("sends signed-out visitors to login", () => {
    renderAt(routes.admin);
    expect(screen.getByText("login page")).toBeInTheDocument();
  });

  it("keeps each role in its own area", () => {
    signInAs("Customer");
    renderAt(routes.admin);
    expect(screen.getByText("customer home")).toBeInTheDocument();
  });

  it("lets an admin into admin", () => {
    signInAs("Admin");
    renderAt(routes.admin);
    expect(screen.getByText("admin dashboard")).toBeInTheDocument();
  });

  it("redirects a signed-in provider from login to their workspace", () => {
    signInAs("Provider");
    renderAt(routes.login);
    expect(screen.getByText("instructor schedule")).toBeInTheDocument();
  });
});
