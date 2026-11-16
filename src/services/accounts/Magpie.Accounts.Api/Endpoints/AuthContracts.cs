namespace Magpie.Accounts.Api.Endpoints;

// The JSON shapes the browser sends and receives. They live in the Api layer
// because they describe HTTP messages, not business concepts.
// Every request property is nullable: we never trust that the caller sent it.

public sealed record RegisterRequest(string? Email, string? Password);

public sealed record ResendConfirmationRequest(string? Email);

public sealed record ConfirmEmailRequest(string? ReaderId, string? Token);

public sealed record SignInRequest(string? Email, string? Password);

public sealed record ForgotPasswordRequest(string? Email);

public sealed record ResetPasswordRequest(string? ReaderId, string? Token, string? NewPassword);

public sealed record AntiforgeryTokenResponse(string Token);

public sealed record CurrentReaderResponse(string Id, string Email);
