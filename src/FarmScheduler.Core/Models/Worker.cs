namespace FarmScheduler.Core.Models;

public class Worker
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsAdmin { get; set; } = false;
}
