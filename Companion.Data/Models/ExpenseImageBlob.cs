namespace Companion.Data.Models;

public class ExpenseImageBlob
{
    public int ExpenseImageId { get; set; }
    public ExpenseImage Image { get; set; } = null!;
    public required byte[] Content { get; set; }
}