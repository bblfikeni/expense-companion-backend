namespace Companion.Data.Models;

public class WorkspaceMembership
{
    public int WorkspaceId { get; set; }
    public int ProfileId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public Profile Profile { get; set; } = null!;
}