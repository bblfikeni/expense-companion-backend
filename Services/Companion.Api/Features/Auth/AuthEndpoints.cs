using Companion.Api.Dtos;
using Companion.Api.Services;

namespace Companion.Api.Features.Auth;

internal static class AuthEndpoints
{
    internal const string RateLimitPolicy = "auth";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").RequireRateLimiting(RateLimitPolicy);

        group.MapPost("/register",
            async (RegisterRequest request, IAuthenticationService auth, CancellationToken ct) =>
            {
                var result = await auth.RegisterAsync(request, ct);
                return result switch
                {
                    { Success: true } => Results.Accepted("", new { result.Email, result.Success }),
                    { Success: false, ConsentGiven: false } => Results.Problem(
                        statusCode: 400,
                        title: "Consent required",
                        detail: "POPIA consent must be given to register.",
                        extensions: new Dictionary<string, object?> { ["consent"] = false }),
                    _ => Results.Problem(statusCode: 400, title: "Registration failed")
                };
            })
            .WithName("Register");

        group.MapPost("/verify-email",
            async (VerifyEmailRequest request, IAuthenticationService auth, CancellationToken ct) =>
            {
                var result = await auth.VerifyEmailAsync(request.Email, request.Code, request.Password, ct);
                return result.Success
                    ? Results.NoContent()
                    : Results.Problem(statusCode: 400, title: "Verification failed", detail: "Invalid or expired code.");
            })
            .WithName("VerifyEmail");

        group.MapPost("/resend-verification",
            async (ResendVerificationRequest request, IAuthenticationService auth, CancellationToken ct) =>
            {
                await auth.ResendVerificationAsync(request.Email, ct);
                return Results.Accepted("", new { Success = true });
            })
            .WithName("ResendVerification");

        group.MapPost("/login",
            async (LoginRequest request, IAuthenticationService auth, CancellationToken ct) =>
            {
                var result = await auth.LoginAsync(request.Email, request.Password, ct);
                return result.Success
                    ? Results.Ok(new { result.AccessToken, result.ExpiresInSeconds, result.TokenType })
                    : Results.Problem(statusCode: 401, title: "Login failed", detail: "Invalid email or password.");
            })
            .WithName("Login");

        group.MapPost("/google",
            async (GoogleLoginRequest request, IAuthenticationService auth, CancellationToken ct) =>
            {
                var result = await auth.GoogleLoginAsync(request.IdToken, request.PopiaConsent, ct);
                return result switch
                {
                    { Success: true } => Results.Ok(new
                    {
                        result.AccessToken,
                        result.ExpiresInSeconds,
                        result.TokenType,
                        result.IsNewUser,
                    }),
                    { RequiresConsent: true } => Results.Problem(
                        statusCode: 400,
                        title: "Consent required",
                        detail: "POPIA consent must be given to create an account.",
                        extensions: new Dictionary<string, object?> { ["consent"] = false }),
                    _ => Results.Problem(statusCode: 401, title: "Google authentication failed")
                };
            })
            .WithName("GoogleLogin");

        group.MapPost("/forgot-password",
            async (ForgotPasswordRequest request, IAuthenticationService auth, CancellationToken ct) =>
            {
                var result = await auth.ForgotPasswordAsync(request.Email, ct);
                return Results.Accepted("", new { result.Email, result.Success });
            })
            .WithName("ForgotPassword");

        group.MapPost("/reset-password",
            async (ResetPasswordRequest request, IAuthenticationService auth, CancellationToken ct) =>
            {
                var result = await auth.ResetPasswordAsync(request.Email, request.Code, request.NewPassword, ct);
                return result.Success
                    ? Results.NoContent()
                    : Results.Problem(statusCode: 400, title: "Reset failed", detail: "Invalid or expired code.");
            })
            .WithName("ResetPassword");
    }
}
