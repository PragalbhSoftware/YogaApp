import { ArrowLeft } from "lucide-react";
import { Alert, Button } from "@mui/material";
import { OtpBoxes } from "@/features/auth/components/otp-boxes";
import { formatIstExpiry } from "@/features/auth/utils/format-expiry";
import { PhoneText } from "@/components/common/phone-text";

type OtpStepProps = {
  phone: string;
  code: string;
  expiresAt?: string;
  devCode?: string | null;
  error: string | null;
  busy: boolean;
  onCodeChange: (value: string) => void;
  onVerify: () => void;
  onResend: () => void;
  onChangePhone: () => void;
};

export function OtpStep({
  phone,
  code,
  expiresAt,
  devCode,
  error,
  busy,
  onCodeChange,
  onVerify,
  onResend,
  onChangePhone,
}: OtpStepProps) {
  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h2 className="font-heading text-2xl font-medium sm:text-3xl">Enter the code</h2>
        <p className="text-sm leading-relaxed text-brand-muted">
          We sent a code to <PhoneText className="font-medium text-brand-text" value={phone} />.
        </p>
        {expiresAt ? (
          <p className="text-xs text-brand-muted">Expires at {formatIstExpiry(expiresAt)} IST.</p>
        ) : null}
      </header>

      {devCode ? (
        <p className="rounded-xl bg-brand-secondary/20 px-3 py-2 text-xs">
          Development code: <span className="font-medium">{devCode}</span>
        </p>
      ) : null}

      {error ? <Alert severity="error">{error}</Alert> : null}

      <form
        className="space-y-7"
        onSubmit={(event) => {
          event.preventDefault();
          onVerify();
        }}
      >
        <div className="space-y-2">
          <p className="text-sm font-medium text-brand-text">Verification code</p>
          <OtpBoxes value={code} onChange={onCodeChange} disabled={busy} />
        </div>
        <div className="pt-2">
          <Button
            type="submit"
            variant="contained"
            size="large"
            fullWidth
            disabled={busy}
            sx={{ minHeight: 52, borderRadius: "14px" }}
          >
            {busy ? "Verifying…" : "Verify and continue"}
          </Button>
        </div>
      </form>

      <div className="flex flex-col-reverse gap-3 sm:flex-row sm:flex-wrap sm:items-center sm:justify-between">
        <Button
          type="button"
          variant="text"
          startIcon={<ArrowLeft size={16} />}
          onClick={onChangePhone}
          sx={{ width: { xs: "100%", sm: "auto" } }}
        >
          Use a different phone
        </Button>
        <Button
          type="button"
          variant="outlined"
          disabled={busy}
          onClick={onResend}
          sx={{ width: { xs: "100%", sm: "auto" } }}
        >
          Resend code
        </Button>
      </div>
    </div>
  );
}
