using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Companion.Api.Tests.Infrastructure;
using Companion.Data;
using Companion.Data.Extensions;
using Companion.Data.Models;
using Companion.Events;
using Companion.Extraction;
using Companion.Extraction.Configuration;
using Companion.Extraction.Ocr;
using Companion.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ExtractionPipelineVerificationTests(PostgresFixture postgres)
{
    private const string Vendor = "Hemlock Test Pantry";
    private const string PrintedTotal = "R928.14";
    private const string PrintedVat = "R64.72";
    private const string VatNumber = "4123456798";
    private const string Marker = "OCR_PRIVATE_MARKER";
    private const string FileName = "synthetic-private-receipt-file.png";

    private static readonly string[] ReceiptLines =
        [Vendor, "Tax Invoice", "Date 2026-01-02", $"VAT No {VatNumber}", $"Total {PrintedTotal}", $"VAT {PrintedVat}", Marker];
    private static readonly byte[] ImageBytes = "private-image-blob-token"u8.ToArray();

    [Fact]
    public async Task RedeliverySkipsCompletedHashAndNonProcessingExpenseAndPageOrderChangesHash()
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        var first = new byte[] { 1, 2, 3 };
        var second = new byte[] { 4, 5, 6 };
        var processing = await SeedAsync(factory, "Processing", first, second);
        var reversed = await SeedAsync(factory, "Processing", second, first);
        var draft = await SeedAsync(factory, "Uploaded", first);
        var ocr = new RecordingOcr();
        var settings = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions { Enabled = true });
        var processor = new ReceiptExtractionProcessor(factory, ocr, settings, TimeProvider.System,
            NullLogger<ReceiptExtractionProcessor>.Instance);
        var bus = new ExtractionWorkerTests.RecordingBus();
        using var worker = new ExtractionWorker(bus, processor, settings, NullLogger<ExtractionWorker>.Instance);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await worker.StartAsync(watchdog.Token);
        try
        {
            await bus.Subscribed.Task.WaitAsync(watchdog.Token);
            var firstEvent = Event(processing.PublicId);
            await bus.DeliverAsync(firstEvent, watchdog.Token);
            await bus.DeliverAsync(firstEvent, watchdog.Token);
            await bus.DeliverAsync(Event(draft.PublicId), watchdog.Token);
            Assert.Equal(2, ocr.Calls);

            await using var db = await factory.CreateDbContextAsync(watchdog.Token);
            Assert.Equal(SetHash(first, second), (await db.ExpenseExtractions.SingleAsync(watchdog.Token)).ImageSetHash);
            await bus.DeliverAsync(Event(reversed.PublicId), watchdog.Token);
            Assert.Equal(4, ocr.Calls);
            Assert.Equal(0, bus.PublishCalls);
            Assert.NotEqual(SetHash(first, second), SetHash(second, first));
            Assert.Equal(SetHash(second, first), (await db.ExpenseExtractions.SingleAsync(
                row => row.ExpenseId == reversed.Id, watchdog.Token)).ImageSetHash);
            Assert.Equal(4, await db.ExpenseImageResults.CountAsync(watchdog.Token));
        }
        finally
        {
            await worker.StopAsync(watchdog.Token);
        }
    }

    [Theory]
    [InlineData("User_Manual")]
    [InlineData("User_Override")]
    public async Task UserOwnedValuesRemainUntouchedAndSuggestionsStayInExtraction(string entryOrigin)
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        var expense = await SeedAsync(factory, "Processing", ImageBytes);
        await using var db = await factory.CreateDbContextAsync();
        // This schema has no user-owned expense fields yet. Add them in this database
        // only to prove the processor leaves such fields untouched when present.
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE expenses ADD COLUMN entry_origin text, ADD COLUMN vendor text,
                ADD COLUMN expense_date text, ADD COLUMN total_amount_cents bigint, ADD COLUMN vat_amount_cents bigint
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE expenses SET entry_origin = {entryOrigin}, vendor = 'Manual Demo Vendor',
                expense_date = '2026-01-01', total_amount_cents = 5000, vat_amount_cents = 100
            WHERE id = {expense.Id}
            """);

        var ocr = new RecordingOcr();
        var settings = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions());
        var processor = new ReceiptExtractionProcessor(factory, ocr, settings, TimeProvider.System,
            NullLogger<ReceiptExtractionProcessor>.Instance);
        await processor.ProcessAsync(Event(expense.PublicId), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        var original = await db.Database.SqlQuery<string>($"""
            SELECT json_build_array(entry_origin, vendor, expense_date, total_amount_cents, vat_amount_cents)::text AS "Value"
            FROM expenses WHERE id = {expense.Id}
            """).SingleAsync();
        using var values = JsonDocument.Parse(original);
        Assert.Equal(entryOrigin, values.RootElement[0].GetString());
        Assert.Equal("Manual Demo Vendor", values.RootElement[1].GetString());
        Assert.Equal("2026-01-01", values.RootElement[2].GetString());
        Assert.Equal(5000, values.RootElement[3].GetInt64());
        Assert.Equal(100, values.RootElement[4].GetInt64());
        var suggestion = await db.ExpenseExtractions.SingleAsync();
        Assert.Equal(Vendor, suggestion.Vendor);
        Assert.Equal("2026-01-02", suggestion.ExpenseDate);
        Assert.Equal(92814, suggestion.TotalAmountCents);
        Assert.Equal(6472, suggestion.VatAmountCents);
    }

    [UnixTheory]
    [InlineData("success", "NeedsReview")]
    [InlineData("timeout", "Failed")]
    [InlineData("parse-failure", "NeedsReview")]
    public async Task FullProcessingLogsExcludeReceiptData(string scenario, string expectedStatus)
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var admin = CompanionHosts.Build(connectionString);
        await admin.ApplyCompanionMigrationsAsync();
        var factory = admin.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        var expense = await SeedAsync(factory, "Processing", ImageBytes);
        var directory = Directory.CreateTempSubdirectory("companion-privacy-test-");
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            var executable = Path.Combine(directory.FullName, FileName);
            var lines = scenario == "parse-failure" ? new[] { "??", "123", Marker } : ReceiptLines;
            var script = "#!/bin/sh\ncat > /dev/null\nprintf '%s\\n' '" + FileName + "' >&2\n";
            script += "cat <<'TSV'\n" + ToTsv(lines) + "\nTSV\n";
            if (scenario == "timeout") script += "exec sleep 600\n";
            await File.WriteAllTextAsync(executable, script, watchdog.Token);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            using var logs = new RecordingLogProvider();
            builder.Logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Extraction:Enabled"] = "true",
                ["Extraction:MessageTimeoutSeconds"] = "10",
                ["Extraction:OcrExecutablePath"] = executable,
                ["Extraction:OcrTimeoutSeconds"] = scenario == "timeout" ? "1" : "5",
                ["Extraction:MaxRetries"] = "1",
                ["ConnectionStrings:ExtractionDb"] = connectionString,
                ["RabbitMq:ConnectionString"] = "host=localhost",
            });
            Companion.Extraction.Program.Configure(builder);
            var bus = new ExtractionWorkerTests.RecordingBus();
            builder.Services.AddSingleton<ICompanionMessageBus>(bus);
            using var host = builder.Build();
            await host.StartAsync(watchdog.Token);
            await bus.Subscribed.Task.WaitAsync(watchdog.Token);
            await bus.DeliverAsync(Event(expense.PublicId), watchdog.Token);
            await host.StopAsync(watchdog.Token);

            await using var verify = await factory.CreateDbContextAsync(watchdog.Token);
            var result = await verify.ExpenseExtractions.SingleAsync(watchdog.Token);
            Assert.Equal(expectedStatus, result.Status);
            Assert.Equal(scenario == "success" ? 92814L : (long?)null, result.TotalAmountCents);
            Assert.Contains(logs.Messages, message => message.Contains("Receipt extraction started", StringComparison.Ordinal));
            if (scenario == "timeout")
                Assert.Contains(logs.Messages, message => message.Contains("OcrTimeout", StringComparison.Ordinal));
            foreach (var secret in new[]
            {
                Marker, Vendor, PrintedTotal, PrintedVat, "92814", "6472", VatNumber, FileName,
                Encoding.UTF8.GetString(ImageBytes), Convert.ToHexString(ImageBytes), Convert.ToBase64String(ImageBytes),
                string.Join(", ", ImageBytes),
            })
            {
                Assert.DoesNotContain(logs.Messages, message => message.Contains(secret, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<Expense> SeedAsync(IDbContextFactory<CompanionDbContext> factory, string status, params byte[][] images)
    {
        await using var db = await factory.CreateDbContextAsync();
        var profile = new Profile { UserName = Guid.NewGuid().ToString(), FullName = "Extraction test", PopiaConsentDate = DateTimeOffset.UtcNow };
        var expense = new Expense { Workspace = new Workspace { Name = "Extraction verification" }, Status = status };
        db.AddRange(profile, expense);
        await db.SaveChangesAsync();
        for (var page = 0; page < images.Length; page++)
        {
            var bytes = images[page];
            db.ExpenseImages.Add(new ExpenseImage
            {
                ExpenseId = expense.Id, UploadedByProfileId = profile.Id, PageNo = page + 1, Label = "Receipt",
                ContentType = "image/png", SizeBytes = bytes.Length, Sha256 = Hash(bytes),
                Width = 1, Height = 1, CreatedAt = DateTimeOffset.UtcNow,
                Blob = new ExpenseImageBlob { Content = bytes },
            });
        }
        await db.SaveChangesAsync();
        return expense;
    }

    private static string ToTsv(string[] lines) => "1\t1\t0\t0\t0\t0\t0\t0\t300\t300\t-1\t\n" +
        string.Join('\n', lines.SelectMany((line, index) => line.Split(' ').Select((token, word) =>
            FormattableString.Invariant($"5\t1\t1\t1\t{index + 1}\t{word + 1}\t{word * 20}\t{index * 20}\t20\t15\t92.5\t{token}"))));

    private static ReceiptUploadedEvent Event(Guid id) => new() { ExpensePublicId = id, EventId = Guid.NewGuid() };

    private static string SetHash(params byte[][] images) => Hash(Encoding.UTF8.GetBytes(string.Concat(images.Select(Hash))));
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed class RecordingOcr : IOcrEngine
    {
        public int Calls { get; private set; }

        public Task<OcrResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(TesseractTsv.Parse(ToTsv(ReceiptLines), cancellationToken));
        }
    }

    private sealed class RecordingLogProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);
        public void Dispose() { }

        private sealed class RecordingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public bool IsEnabled(LogLevel logLevel) => true;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + "\n" + exception);
        }
    }

    public sealed class UnixTheoryAttribute : TheoryAttribute
    {
        public UnixTheoryAttribute()
        {
            if (OperatingSystem.IsWindows())
                Skip = "The fake executable uses a Unix shell.";
        }
    }
}
