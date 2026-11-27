using System.Security.Cryptography;
using System.Text.Json;
using Companion.Api.Tests.Infrastructure;
using Companion.Data;
using Companion.Data.Extensions;
using Companion.Data.Models;
using Companion.Events;
using Companion.Extraction;
using Companion.Extraction.Configuration;
using Companion.Extraction.Ocr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ExtractionDatabaseRoleTests(PostgresFixture postgres)
{
    [Fact]
    public async Task RestrictedLoginCanClaimRetryAndPersistButCannotAccessPrivateTablesOrWriteExpenses()
    {
        var adminConnection = await postgres.CreateEmptyDatabaseAsync();
        using var adminHost = CompanionHosts.Build(adminConnection);
        await adminHost.ApplyCompanionMigrationsAsync();
        var adminFactory = adminHost.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        var original = new byte[] { 4, 8, 15, 16 };
        var now = DateTimeOffset.UtcNow;
        var expense = new Expense { Workspace = new Workspace { Name = "Role test" }, Status = "Processing" };
        await using (var db = await adminFactory.CreateDbContextAsync())
        {
            var profile = new Profile { UserName = "role-test", FullName = "Role Test", PopiaConsentDate = now };
            db.AddRange(profile, expense);
            await db.SaveChangesAsync();
            db.ExpenseImages.Add(new ExpenseImage
            {
                ExpenseId = expense.Id, UploadedByProfileId = profile.Id, PageNo = 1,
                ContentType = "image/png", SizeBytes = original.Length,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(original)), Width = 1, Height = 1,
                CreatedAt = now, Blob = new ExpenseImageBlob { Content = original },
            });
            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = nameof(ReceiptUploadedEvent),
                Payload = JsonSerializer.Serialize(new { expensePublicId = expense.PublicId }),
                CreatedAt = now, PublishedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await using (var admin = new NpgsqlConnection(adminConnection))
        {
            await admin.OpenAsync();
            var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "extraction-role.sql"));
            await using (var apply = new NpgsqlCommand(sql, admin))
                await apply.ExecuteNonQueryAsync();
            await using var setPassword = new NpgsqlCommand($"ALTER ROLE extraction_svc PASSWORD '{password}'", admin);
            await setPassword.ExecuteNonQueryAsync();
        }

        var restrictedConnection = new NpgsqlConnectionStringBuilder(adminConnection)
        {
            Username = "extraction_svc", Password = password,
        }.ConnectionString;
        await using (var restricted = new NpgsqlConnection(restrictedConnection))
        {
            await restricted.OpenAsync();
            await using var identity = new NpgsqlCommand("SELECT current_user", restricted);
            Assert.Equal("extraction_svc", await identity.ExecuteScalarAsync());
            await using var published = new NpgsqlCommand("SELECT expense_public_id FROM published_receipt_events", restricted);
            Assert.Equal(expense.PublicId.ToString(), await published.ExecuteScalarAsync());

            foreach (var statement in new[]
            {
                "SELECT * FROM profiles",
                "SELECT * FROM profile_tokens",
                "SELECT * FROM otp_codes",
                "SELECT * FROM outbox_messages",
                "DELETE FROM expenses WHERE false",
                "UPDATE expenses SET workspace_id = 1 WHERE false",
                "UPDATE expenses SET capture_request_id = NULL WHERE false",
                "UPDATE expenses SET status = 'Processing' WHERE false",
                "INSERT INTO profiles DEFAULT VALUES",
                "DELETE FROM expense_extractions WHERE false",
                "UPDATE expense_image_results SET page_no = 1 WHERE false",
            })
            {
                await using var denied = new NpgsqlCommand(statement, restricted);
                var failure = await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync());
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
            }
        }

        using var workerHost = CompanionHosts.Build(restrictedConnection);
        var workerFactory = workerHost.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        var fake = new FailOnceOcr(original);
        var options = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions { RetryBackoffBaseSeconds = 0 });
        var processor = new ReceiptExtractionProcessor(workerFactory, fake, options, TimeProvider.System,
            NullLogger<ReceiptExtractionProcessor>.Instance);
        await processor.ProcessAsync(new ReceiptUploadedEvent { ExpensePublicId = expense.PublicId, EventId = Guid.NewGuid() },
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        await using var verify = await adminFactory.CreateDbContextAsync();
        var extraction = await verify.ExpenseExtractions.SingleAsync();
        Assert.Equal("NeedsReview", extraction.Status);
        Assert.Equal(2, extraction.AttemptCount);
        Assert.Equal(2, fake.Calls);
        Assert.Equal(extraction.Id, (await verify.ExpenseImageResults.SingleAsync()).ExtractionId);
    }

    private sealed class FailOnceOcr(byte[] original) : IOcrEngine
    {
        public int Calls { get; private set; }

        public Task<OcrResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            Assert.Equal(original, imageBytes);
            if (++Calls == 1) throw new OcrTimeout();
            return Task.FromResult(new OcrResult());
        }
    }
}
