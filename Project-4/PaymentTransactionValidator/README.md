# PaymentTransactionValidator 

Create a simple C# minimal API application and add to it a _Models_ folder:

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
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.Json
```

Add this section to _appsettings.Development.json_:

```json
"GitHub": {
  "Endpoint": "https://models.github.ai/inference",
  "ApiKey": "PUT-GITHUB-PERSONAL-ACCESS-TOKEN-HERE",
  "model": "gpt-4.1-mini"
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

**Models/Prompt.cs**

```C#
public class Prompt {
    public string? Name { get; set; }
    public string? Instructions { get; set; }
    public string? Description { get; set; }
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

