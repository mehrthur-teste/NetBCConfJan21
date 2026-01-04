using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Caching.Memory;

using ConcurrentWorkflow.Models;
using Microsoft.Agents.AI.Workflows;

using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add MemoryCache
builder.Services.AddMemoryCache();

// Add connection string to Configuration
builder.Services.AddSingleton(sp =>
    builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty
);

// Read configuration values for Azure OpenAI
string endpoint = builder.Configuration["GitHub:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is missing");
string apiKey = builder.Configuration["GitHub:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey configuration is missing");
string model = builder.Configuration["GitHub:model"] ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName configuration is missing");
var app = builder.Build();

IChatClient chatClient = new ChatClient(
    model,
    new AzureKeyCredential(apiKey!),
    new OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint)
    }
)
.AsIChatClient();

app.MapGet("health-check/", () => "Hello World!");

app.MapPost("/create-agent", (Agent AgentConfig, IMemoryCache cache) => {
    var agents = AgentConfig.Prompts!.Select(prompt => 
        new ChatClientAgent(
            chatClient,
            name: prompt.Name!,
            instructions: prompt.Instructions!,
            description: prompt.Description!
        )).ToArray();
    
    // Store agents in cache
    foreach (var agent in agents)
    {
        cache.Set(agent.Id, agent);
    }
    
    var result = new {
        ChatClientId = chatClient.GetHashCode().ToString(),
        Agents = agents.Select(agent => new {
            Id = agent.Id,
            Name = agent.Name,
            Description = agent.Description,
            Instructions = agent.Instructions,
            DisplayName = agent.DisplayName
        }).ToArray()
    };
    
    return Results.Ok(result);
});


app.MapPost("/run-workflow", async (WorkflowRequest request, IMemoryCache cache) => {
    var startExecutor = new ConcurrentStartExecutor();
    
    // Retrieve agents from cache and convert to ExecutorBinding
    var targets = request.AgentIds!.Select(id => 
        (ExecutorBinding)cache.Get<ChatClientAgent>(id)!
    ).ToArray();
    
    var aggregationExecutor = new ConcurrentAggregationExecutor(targets.Length);

    var workflow = new WorkflowBuilder(startExecutor)
        .AddFanOutEdge(startExecutor, targets: targets)
        .AddFanInEdge(sources: targets, aggregationExecutor)
        .WithOutputFrom(aggregationExecutor)
        .Build();

    // Serializar Transactions para string se existir
    string transactionData = request.Transactions != null 
        ? JsonSerializer.Serialize(request.Transactions) 
        : "";
    
    string inputMessage = !string.IsNullOrEmpty(transactionData) 
        ? $"Analyze these transactions: {transactionData}. {request.Question ?? "Are these transactions fraudulent?"}"
        : request.Question ?? "According the agents, are these transactions fraudulent mainly the first object? Just answer using Allow or Block.";

    // Execute workflow and collect result
    await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, inputMessage);
    
    string? result = null;
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        if (evt is WorkflowOutputEvent output)
        {
            result = output.Data?.ToString();
        }
    }
        
    return Results.Ok(new { 
        WorkflowId = workflow.GetHashCode().ToString(),
        Result = result 
    });
});


app.Run();
