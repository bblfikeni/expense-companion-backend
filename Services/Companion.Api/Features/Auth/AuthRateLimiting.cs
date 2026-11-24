using System.Globalization;
using System.Threading.RateLimiting;

namespace Companion.Api.Features.Auth;

internal static class AuthRateLimiting
{
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
        => services.AddRateLimiter(options =>
        {
            options.AddPolicy(AuthEndpoints.RateLimitPolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = RateLimitWindow,
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                    }));

            options.OnRejected = (context, _) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var estimatedDelay)
                    ? estimatedDelay
                    : RateLimitWindow;
                context.HttpContext.Response.Headers.RetryAfter =
                    Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                return new ValueTask(Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Too many authentication requests")
                    .ExecuteAsync(context.HttpContext));
            };
        });
}
