export type User = {
  id: string;
  name: string | null;
  phone: string;
  gender: string | null;
  role: string;
  /** True when the user has not accepted the current Terms and Privacy version. */
  consentRequired?: boolean;
};

export type OtpResponse = {
  challengeId: string;
  expiresAt: string;
  devCode?: string | null;
};

export type VerifyResponse = {
  token: string;
  user: User;
};

export type AccountKind = "existing" | "new";
