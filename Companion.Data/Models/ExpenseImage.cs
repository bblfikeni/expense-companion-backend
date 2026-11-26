namespace Companion.Data.Models;

public class ExpenseImage
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int ExpenseId { get; set; }
    public Expense Expense { get; set; } = null!;
    public int PageNo { get; set; }
    public required string ContentType { get; set; }
    public int SizeBytes { get; set; }
    public required string Sha256 { get; set; }
    public string? Label { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int UploadedByProfileId { get; set; }
    public Profile UploadedByProfile { get; set; } = null!;
    public ExpenseImageBlob Blob { get; set; } = null!;
}