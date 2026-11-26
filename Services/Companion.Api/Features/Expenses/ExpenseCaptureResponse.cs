namespace Companion.Api.Features.Expenses;

public sealed record ExpenseCaptureResponse(Guid ExpenseId, string Status);