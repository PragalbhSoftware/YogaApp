import { useState } from "react";
import { useForm } from "react-hook-form";
import { useNavigate } from "react-router-dom";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { AccountStep } from "@/features/auth/components/account-step";
import { OtpStep } from "@/features/auth/components/otp-step";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { useRequestOtp, useResendOtp, useVerifyOtp } from "@/features/auth/hooks/use-auth-mutations";
import { accountSchema, type AccountFormValues } from "@/features/auth/schemas";
import type { OtpResponse } from "@/features/auth/types";
import { toUserMessage } from "@/services/http/api-error";
import { afterSignInPath } from "@/constants/routes";

export function LoginForm() {
  const navigate = useNavigate();
  const { signIn } = useAuth();
  const [step, setStep] = useState<"account" | "otp">("account");
  const [otp, setOtp] = useState<OtpResponse | null>(null);
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);

  const form = useForm<AccountFormValues>({
    resolver: zodResolver(accountSchema),
    defaultValues: {
      accountKind: "existing",
      phone: "",
      name: "",
      gender: "",
    },
  });

  const phone = form.watch("phone");
  const requestOtp = useRequestOtp();
  const resendOtp = useResendOtp();
  const verifyOtp = useVerifyOtp();
  const busy = requestOtp.isPending || resendOtp.isPending || verifyOtp.isPending;

  function applyChallenge(data: OtpResponse) {
    setError(null);
    setOtp(data);
    setCode(data.devCode ?? "");
    setStep("otp");
  }

  function onRequest(values: AccountFormValues) {
    setError(null);
    requestOtp.mutate(
      {
        phone: values.phone,
        isNewUser: values.accountKind === "new",
        name: values.accountKind === "new" ? values.name : undefined,
        gender: values.accountKind === "new" ? values.gender : undefined,
      },
      {
        onSuccess: applyChallenge,
        onError: (err) => setError(toUserMessage(err)),
      },
    );
  }

  function onVerify() {
    if (code.trim().length !== 6) {
      setError("Enter the code we sent.");
      return;
    }
    setError(null);
    verifyOtp.mutate(
      { phone, code: code.trim() },
      {
        onSuccess: (data) => {
          signIn(data.token, data.user);
          toast.success("Signed in.");
          navigate(afterSignInPath(data.user.role), { replace: true });
        },
        onError: (err) => setError(toUserMessage(err)),
      },
    );
  }

  function onResend() {
    resendOtp.mutate(phone, {
      onSuccess: (data) => {
        applyChallenge(data);
        toast.success("A new code was sent.");
      },
      onError: (err) => setError(toUserMessage(err)),
    });
  }

  return (
    <div className="w-full">
      {step === "account" ? (
        <AccountStep form={form} error={error} busy={busy} onSubmit={onRequest} />
      ) : (
        <OtpStep
          phone={phone}
          code={code}
          expiresAt={otp?.expiresAt}
          devCode={otp?.devCode}
          error={error}
          busy={busy}
          onCodeChange={setCode}
          onVerify={onVerify}
          onResend={onResend}
          onChangePhone={() => {
            setStep("account");
            setError(null);
            setCode("");
          }}
        />
      )}
    </div>
  );
}
