// Frontend mirrors of the backend auth contracts.
// Source of truth: src/Favourites.Api/Contracts/Auth/AuthContracts.cs
// JSON over the wire is camelCase (ASP.NET Core default).

export interface RegisterRequest {
  readonly displayName: string;
  readonly email: string;
  readonly password: string;
  readonly confirmPassword: string;
}

export interface RegisterResponse {
  readonly id: string;
  readonly displayName: string;
  readonly email: string;
}

export interface LoginRequest {
  readonly email: string;
  readonly password: string;
  readonly rememberMe: boolean;
}

export interface LoginResponse {
  readonly id: string;
  readonly displayName: string;
  readonly email: string;
}

export interface LogoutResponse {
  readonly succeeded: boolean;
}

export interface CurrentUserResponse {
  readonly id: string;
  readonly displayName: string;
  readonly email: string;
}

/** Locally-held projection of the authenticated user. */
export interface AuthUser {
  readonly id: string;
  readonly displayName: string;
  readonly email: string;
}

export interface ForgotPasswordRequest {
  readonly email: string;
}

export interface ResetPasswordRequest {
  readonly token: string;
  readonly newPassword: string;
  readonly confirmNewPassword: string;
}
