namespace Triply.Api.Modules.Auth.Dtos;

public record RegisterRequest(string Email, string Password, string? DisplayName);

public record LoginRequest(string Email, string Password);

public record AuthResponse(
    string Token,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    string Email,
    string? DisplayName);

// ---- Refresh / logout (Security Task 1) ----

public record RefreshTokenRequest(string RefreshToken);

// ---- Email verification ----

public record ConfirmEmailRequest(Guid UserId, string Token);

public record ResendConfirmationRequest(string Email);

// ---- Password reset ----

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);
