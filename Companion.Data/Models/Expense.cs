namespace Companion.Data.Models;

public class Expense
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public string Status { get; set; } = "None";
    public Guid? CaptureRequestId { get; set; }
}