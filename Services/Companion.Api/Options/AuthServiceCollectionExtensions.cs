using System.Security.Claims;
using Companion.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Companion.Api.Options;
using Companion.Api.Services;
using Companion.Data;
using Companion.Data.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the gateway authentication services: <see cref="OtpService"/>, <see cref="AuthenticationService"/>,
/// JWT Bearer authentication, CORS and ProblemDetails. Called by <c>Companion.Api</c> only.
/// Depends on <see cref="AuthServiceCollectionExtensions.AddAuthOptions</c> having already
/// registered <see cref="AuthOptions"/> as a singleton.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);

        // ---- OtpService: pepper from secret config ----
        var pepperRaw = configuration["Auth:Otps:Pepper"]
            ?? throw new InvalidOperationException(
                "Auth:Otps:Pepper is required. Set a base64-encoded 32+ byte value, e.g. openssl rand -base64 48.");

        byte[] pepper;
        try
        {
            pepper = Convert.FromBase64String(pepperRaw);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            throw new InvalidOperationException(
                "Auth:Otps:Pepper must be base64-encoded 32+ bytes (256+ bits).", ex);
        }

        if (pepper.Length < 32)
        {
            throw new InvalidOperationException(
                "Auth:Otps:Pepper must be base64-encoded 32+ bytes (256+ bits).");
        }

        services.AddTransient<IOtpService>(sp => new OtpService(
            sp.GetRequiredService<CompanionDbContext>(),
            sp.GetRequiredService<AuthOptions>().Otps,
            pepper,
            sp.GetRequiredService<TimeProvider>()));

        // ---- AuthenticationService ----
        services.AddTransient<IAuthenticationService, AuthenticationService>();

        // ---- JWT Bearer ----
        var jwtIssuer = configuration["Auth:Jwt:Issuer"] ?? string.Empty;
        var jwtAudience = configuration["Auth:Jwt:Audience"] ?? string.Empty;
        var jwtSigningKey = configuration["Auth:Jwt:SigningKey"] ?? string.Empty;
        var signingKey = Convert.FromBase64String(jwtSigningKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var stamp = context.Principal?.FindFirstValue("security_stamp");
                    if (!Guid.TryParse(subject, out var publicId) || string.IsNullOrEmpty(stamp))
                    {
                        context.Fail("Invalid account claims.");
                        return;
                    }
                    var db = context.HttpContext.RequestServices.GetRequiredService<CompanionDbContext>();
                    if (!await db.Users.AsNoTracking().AnyAsync(
                        p => p.PublicId == publicId && p.SecurityStamp == stamp && !p.IsAnonymized,
                        context.HttpContext.RequestAborted))
                        context.Fail("Account credentials have changed.");
                },
            };
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(signingKey),
                ClockSkew = TimeSpan.Zero,
            };
        });

        // ---- CORS - exact configured origin, no wildcard ----
        var corsOrigin = configuration["Cors:AllowedOrigin"]
            ?? throw new InvalidOperationException("Cors:AllowedOrigin is required.");

        services.AddCors(options =>
        {
            options.AddPolicy("companion-web", policy =>
            {
                policy.WithOrigins(corsOrigin)
                    .AllowAnyHeader()
                    .WithMethods("GET", "PUT", "POST", "DELETE", "OPTIONS");
            });
        });

        // ---- ProblemDetails ----
        services.AddProblemDetails();

        return services;
    }
}

/// <summary>
/// Binds <c>Auth</c> configuration section into <see cref="AuthOptions"/> and registers
/// the configuration as a singleton so it can be injected anywhere.
/// </summary>
public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAuthOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var auth = new AuthOptions();
        configuration.GetSection("Auth").Bind(auth);

        var rawKey = auth.Jwt.SigningKey;

        // Validate the JWT signing key as early as possible. A key shorter than 32 bytes
        // cannot provide the required 256-bit strength for signature validation and would
        // otherwise surface as a confusing token validation failure later.
        byte[]? keyBytes = null;
        try
        {
            keyBytes = Convert.FromBase64String(rawKey);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            throw new InvalidOperationException(
                "Auth:Jwt:SigningKey must be base64-encoded 32+ bytes (256+ bits), e.g. a 44-character base64 string. " +
                "Generate one with: openssl rand -base64 48.");
        }

        if (keyBytes is null || keyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "Auth:Jwt:SigningKey must be base64-encoded 32+ bytes (256+ bits), e.g. a 44-character base64 string. " +
                "Generate one with: openssl rand -base64 48.");
        }

        if (string.IsNullOrWhiteSpace(auth.Jwt.Issuer) || string.IsNullOrWhiteSpace(auth.Jwt.Audience))
            throw new InvalidOperationException("Auth:Jwt:Issuer and Auth:Jwt:Audience are required.");
        if (auth.Otps.ExpiresInMinutes <= 0 || auth.Otps.MaxAttempts <= 0 || auth.Otps.ResendCooldownInSeconds < 0)
            throw new InvalidOperationException("OTP expiry and attempt limit must be positive; cooldown must be nonnegative.");

        services.AddSingleton(auth);
        services.Configure<AuthOptions>(options =>
        {
            options.Jwt = auth.Jwt;
            options.Otps = auth.Otps;
        });
        return services;
    }
}
