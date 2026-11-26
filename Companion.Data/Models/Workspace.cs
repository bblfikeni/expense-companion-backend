namespace Companion.Data.Models;

public class Workspace
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public required string Name { get; set; }
}