using System.Text.Json;

public class WorkflowRequest
{
    public string[]? AgentIds { get; set; } 
    public string? Question { get; set; }
    public JsonElement? Transactions { get; set; } 
}

