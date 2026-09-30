export type User = {
  id: string;
  name: string | null;
  phone: string;
  gender: string | null;
  role: string;
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
