# PaymentTransactionValidator

Create a simple C# minimal API application and add to it a *Models* folder:

```bash
dotnet new webapi -n PaymentTransactionValidator
cd PaymentTransactionValidator
mkdir Models
```

Add required nuget packages:

```bash
dotnet add package Azure.AI.OpenAI -v 2.7.0-beta.2
dotnet add package Azure.Identity -v 1.17.1
dotnet add package Microsoft.Agents.AI.OpenAI -v 1.0.0-preview.251204.1
dotnet add package Microsoft.Agents.AI.Workflows -v 1.0.0-preview.251204.1
dotnet add package Microsoft.Extensions.AI.OpenAI -v 10.1.0-preview.1.25608.1
```

Add this section to *appsettings.Development.json*:

```json
"GitHub": {
  "Endpoint": "https://models.github.ai/inference",
  "ApiKey": "PUT-GITHUB-PERSONAL-ACCESS-TOKEN-HERE",
  "model": "gpt-4.1-mini"
}
```

**Models/Prompt.cs**

```C#
public class Prompt {
    public string? Name { get; set; }
    public string? Instructions { get; set; }
    public string? Description { get; set; }
}
```

**Models/Agent.cs**

```C#
public class Agent {
    public List<Prompt>? Prompts { get; set; }
}
```

**Models/WorkflowRequest.cs**

```C#
using System.Text.Json;

public class WorkflowRequest {
    public string[]? AgentIds { get; set; } 
    public string? Question { get; set; }
    public JsonElement? Transactions { get; set; } 
}
```

**Models/ConcurrentAggregationExecutor.cs**

```C#
using System;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ConcurrentWorkflow.Models;

/// <summary>
/// Executor that aggregates the results from the concurrent agents.
/// </summary>
internal sealed class ConcurrentAggregationExecutor(int expectedAgentCount = 2) :
    Executor<List<ChatMessage>>("ConcurrentAggregationExecutor") {
    private readonly List<ChatMessage> _messages = [];
    private readonly int _expectedAgentCount = expectedAgentCount;

    /// <summary>
    /// Handles incoming messages from the agents and aggregates their responses.
    /// </summary>
    /// <param name="message">The message from the agent</param>
    /// <param name="context">Workflow context for accessing workflow services and adding events</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.
    /// The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public override async ValueTask HandleAsync(List<ChatMessage> message, IWorkflowContext context, CancellationToken cancellationToken = default) {
        this._messages.AddRange(message);

        if (this._messages.Count == _expectedAgentCount) {
            var formattedMessages = string.Join(Environment.NewLine,
                this._messages.Select(m => $"{m.AuthorName}: {m.Text}"));
            await context.YieldOutputAsync(formattedMessages, cancellationToken);
        }
    }
}
```

**Models/ConcurrentStartExecutor.cs**

```C#
using System;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ConcurrentWorkflow.Models;

/// <summary>
/// Executor that starts the concurrent processing by sending messages to the agents.
/// </summary>
internal sealed class ConcurrentStartExecutor() : Executor<string>("ConcurrentStartExecutor") {
    /// <summary>
    /// Starts the concurrent processing by sending messages to the agents.
    /// </summary>
    /// <param name="message">The user message to process</param>
    /// <param name="context">Workflow context for accessing workflow services and adding events</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.
    /// The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public override async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default) {
        // Broadcast the message to all connected agents. Receiving agents will queue
        // the message but will not start processing until they receive a turn token.
        await context.SendMessageAsync(new ChatMessage(ChatRole.User, message), cancellationToken);

        // Broadcast the turn token to kick off the agents.
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken);
    }
}
```

In *Program.cs*, add this code right under *"var builder = WebApplication.CreateBuilder(args);"*,:

```C#
builder.Services.AddMemoryCache();

// Add connection string to Configuration
builder.Services.AddSingleton(sp =>
    builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty
);

// Read configuration values for Azure OpenAI
string endpoint = builder.Configuration["GitHub:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is missing");
string apiKey = builder.Configuration["GitHub:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey configuration is missing");
string model = builder.Configuration["GitHub:model"] ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName configuration is missing");
```

Also in *Program.cs*, add this code right under _*var app = builder.Build();*:

```C#
IChatClient chatClient = new ChatClient(
    model,
    new AzureKeyCredential(apiKey!),
    new OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint)
    }
)
.AsIChatClient();
```

Delete all the sample code that pertains to weather forecasting in *Program.cs*.

Add this code to *Program.cs* right above *app.Run();*:

```C#
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
```

**Try the application**

In a terminal window in the root folder of the app, run the application with:

```bash
dotnet run
```

Enter this endpoint in your browser:

```
http://localhost:5247/health-check
```

NOTE: you must adjust the port number above with your environment.

Expected output in your browser:

![Health Check expected output](health-check.png)

It would be nice if we can have a suitable API interface to test the other endpoints. We can improve our application so that we have a swagger interface.

Stop the webapi application and add the following *Swashbuckle.AspNetCore* package:

```bash
dotnet add package Swashbuckle.AspNetCore
```

In *Program.cs*, add these services:

```C#
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
```

In the same *Program.cs* file, add this code insode the *"if (app.Environment.IsDevelopment())"* block:

Edit the Properties/launchSettings.json file and make these changes:

1. In both the *http* snd *https* blocks, change the value of _launchBrowser_ from *false* to *true*.
2. Also, in both the *http* snd *https* blocks, add: _"launchUrl": "swagger"_

Let us run our application with from inside the terminal window with:

```bash
dotnet watch
```

This page will automatically load in your browser:

![Swagger UI](swagger-ui.png)

