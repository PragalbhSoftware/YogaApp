import { LoginForm } from "@/features/auth/components/login-form";
import { LoginHero } from "@/features/auth/components/login-hero";

export function LoginPage() {
  return (
    <main className="min-h-svh overflow-x-hidden bg-brand-background">
      <LoginHero />
      <section className="relative px-4 pb-[max(1.5rem,env(safe-area-inset-bottom))] pt-5 sm:px-8 md:-mt-8 md:px-12 md:pb-16">
        <div className="mx-auto w-full max-w-md rounded-3xl bg-brand-surface p-5 shadow-[0_16px_40px_rgba(37,49,39,0.08)] sm:p-7 md:p-8">
          <LoginForm />
        </div>
      </section>
    </main>
  );
}
