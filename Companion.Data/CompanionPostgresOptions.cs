namespace Companion.Data;

/// <summary>
/// Tuning knobs for <c>AddCompanionPostgres</c>. Every property has a production-sane
/// default; the optional <c>configure</c> callback exists so a host can override them
/// without Companion.Data having to know about that host's configuration schema.
/// </summary>
public sealed class CompanionPostgresOptions
{
    /// <summary>
    /// When true, EF Core retries transient failures itself using its own execution
    /// strategy. There is deliberately no Polly (or any other library) wrapping the
    /// database: two retry layers stack multiplicatively and make timeout analysis
    /// impossible.
    /// </summary>
    public bool EnableRetryOnFailure { get; set; } = true;

    /// <summary>Total attempts for a retryable command, including the first.</summary>
    public int MaxRetryCount { get; set; } = 3;

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Off by default. When on, EF logs SQL parameter values, which for this schema
    /// includes password hashes, tokens and consent data.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; }

    public bool EnableDetailedErrors { get; set; }
}
