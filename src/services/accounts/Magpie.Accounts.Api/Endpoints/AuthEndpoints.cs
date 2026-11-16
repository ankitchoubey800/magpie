using System.Security.Claims;
using Magpie.Accounts.Api.Security;
using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Application.PasswordReset;
using Magpie.Accounts.Application.Registration;
using Magpie.Accounts.Application.SignIn;
using Microsoft.AspNetCore.Antiforgery;

namespace Magpie.Accounts.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // A group shares a prefix and a filter: every endpoint below starts
        // with /api/accounts and is protected by the anti-forgery check.
        var group = app.MapGroup("/api/accounts").AddEndpointFilter<AntiforgeryFilter>();

        // The web app calls this before its first POST. It sets the anti-forgery
        // cookie and returns the matching token for the X-XSRF-TOKEN header.
        group.MapGet("/antiforgery-token", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return TypedResults.Ok(new AntiforgeryTokenResponse(tokens.RequestToken!));
        });

        group.MapPost("/register", async (
            RegisterRequest request, RegisterReaderHandler handler, CancellationToken cancellationToken) =>
        {
            var outcome = await handler.HandleAsync(request.Email, request.Password, cancellationToken);

            // 202 Accepted = "we took your request; the next step happens elsewhere (your inbox)".
            return outcome.Accepted
                ? Results.Accepted()
                : Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["registration"] = [.. outcome.Errors] });
        }).RequireRateLimiting(AuthRateLimiting.PolicyName);

        group.MapPost("/resend-confirmation", async (
            ResendConfirmationRequest request, ResendConfirmationHandler handler, CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(request.Email, cancellationToken);
            return Results.Accepted();
        }).RequireRateLimiting(AuthRateLimiting.PolicyName);

        group.MapPost("/confirm-email", async (
            ConfirmEmailRequest request, ConfirmEmailHandler handler, CancellationToken cancellationToken) =>
        {
            var confirmed = await handler.HandleAsync(request.ReaderId, request.Token, cancellationToken);

            return confirmed
                ? Results.NoContent()
                : Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "This confirmation link does not work",
                    detail: "It may have expired or been copied incompletely. Ask for a new one.");
        }).RequireRateLimiting(AuthRateLimiting.PolicyName);

        group.MapPost("/sign-in", async (
            SignInRequest request, SignInHandler handler, CancellationToken cancellationToken) =>
        {
            var signedIn = await handler.HandleAsync(request.Email, request.Password, cancellationToken);

            // ONE message for every failure. Do not "improve" it by adding detail.
            return signedIn
                ? Results.NoContent()
                : Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Sign-in failed",
                    detail: "Check your email and password. If you have just registered, confirm your email first. " +
                            "After several failed attempts you must wait a few minutes.");
        }).RequireRateLimiting(AuthRateLimiting.PolicyName);

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest request, RequestPasswordResetHandler handler, CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(request.Email, cancellationToken);
            return Results.Accepted();
        }).RequireRateLimiting(AuthRateLimiting.PolicyName);

        group.MapPost("/reset-password", async (
            ResetPasswordRequest request, ResetPasswordHandler handler, CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                request.ReaderId, request.Token, request.NewPassword, cancellationToken);

            return result.Status switch
            {
                ResetPasswordStatus.Done => Results.NoContent(),
                ResetPasswordStatus.RejectedPassword => Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["password"] = [.. result.Errors] }),
                _ => Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "This reset link does not work",
                    detail: "It may have expired or already been used. Ask for a new one."),
            };
        }).RequireRateLimiting(AuthRateLimiting.PolicyName);

        // No use-case class here: there is nothing to decide, only a cookie to remove.
        group.MapPost("/sign-out", async (IReaderAccounts accounts, CancellationToken cancellationToken) =>
        {
            await accounts.SignOutAsync(cancellationToken);
            return Results.NoContent();
        });

        // "Who am I?" The answer is read from the cookie that came with the
        // request; the database is not touched. RequireAuthorization means:
        // no valid cookie, no entry (401).
        group.MapGet("/me", (ClaimsPrincipal user) => TypedResults.Ok(new CurrentReaderResponse(
                user.FindFirstValue(ClaimTypes.NameIdentifier)!,
                user.FindFirstValue(ClaimTypes.Email)!)))
            .RequireAuthorization();

        return app;
    }
}
