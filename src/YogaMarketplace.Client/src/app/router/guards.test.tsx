import { act, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it } from "vitest";
import { AllowGuestOr, RedirectIfSignedIn, RequireAuth, RequireRole } from "@/app/router/guards";
import { bookPath, routes, signInReturnPath } from "@/constants/routes";
import { useAuthStore } from "@/stores/auth-store";

function BookPageStub() {
  const location = useLocation();
  return <p>book {location.pathname + location.search}</p>;
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<RedirectIfSignedIn />}>
          <Route path={routes.login} element={<p>login page</p>} />
        </Route>
        <Route element={<AllowGuestOr roles={["Customer"]} />}>
          <Route path={routes.home} element={<p>customer home</p>} />
          <Route path={routes.teacher} element={<p>instructor profile</p>} />
          <Route element={<RequireAuth />}>
            <Route element={<RequireRole roles={["Customer"]} />}>
              <Route path={routes.book} element={<BookPageStub />} />
            </Route>
          </Route>
        </Route>
        <Route element={<RequireAuth />}>
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

  it("sends signed-out visitors to login for private areas", () => {
    renderAt(routes.admin);
    expect(screen.getByText("login page")).toBeInTheDocument();
  });

  it("lets signed-out visitors browse and view an instructor", () => {
    renderAt(routes.home);
    expect(screen.getByText("customer home")).toBeInTheDocument();
  });

  it("asks for sign-in at Book and then returns to the same instructor and slot", () => {
    const slotUrl = bookPath("p1", "s1", "Studio");
    renderAt(slotUrl);
    expect(screen.getByText("login page")).toBeInTheDocument();

    act(() => signInAs("Customer"));
    expect(screen.getByText(`book ${slotUrl}`)).toBeInTheDocument();
  });

  it("keeps each role in its own area", () => {
    signInAs("Customer");
    renderAt(routes.admin);
    expect(screen.getByText("customer home")).toBeInTheDocument();
  });

  it("sends an instructor from public browse to their workspace", () => {
    signInAs("Provider");
    renderAt("/teachers/p1");
    expect(screen.getByText("instructor schedule")).toBeInTheDocument();
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

describe("signInReturnPath", () => {
  it("returns a customer to the page that asked them to sign in", () => {
    expect(signInReturnPath("Customer", { pathname: "/book/p1/s1", search: "?mode=Home" })).toBe("/book/p1/s1?mode=Home");
  });

  it("ignores unsafe or other-role paths", () => {
    expect(signInReturnPath("Customer", { pathname: "//evil.example" })).toBe(routes.home);
    expect(signInReturnPath("Customer", { pathname: routes.admin })).toBe(routes.home);
    expect(signInReturnPath("Customer", { pathname: routes.login })).toBe(routes.home);
    expect(signInReturnPath("Provider", { pathname: "/book/p1/s1" })).toBe(routes.instructor);
  });
});
