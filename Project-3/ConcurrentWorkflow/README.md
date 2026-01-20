
### 1. Create Console Application
```bash
dotnet new console -n ConcurrentWorkflow
cd ConcurrentWorkflow
```

### 2. Add Required Packages
```bash
dotnet add package Azure.AI.OpenAI --version 2.7.0-beta.2
dotnet add package Azure.Identity --version 1.17.1
dotnet add package Microsoft.Agents.AI.Workflows --version 1.0.0-preview.251204.1
dotnet add package Microsoft.Extensions.AI.OpenAI --version 10.1.0-preview.1.25608.1
dotnet add package Microsoft.Extensions.Configuration --version 10.0.1
dotnet add package Microsoft.Extensions.Configuration.Json --version 10.0.1
```

### 3. Configure _appsettings.json_.

**appsettings.json** - GitHub Models configuration

```json
{
    "GitHub": {
        "Token": "PUT-GITHUB-PERSONAL-ACCESS-TOKEN-HERE",
        "ApiEndpoint": "https://models.github.ai/inference",
        "Model": "openai/gpt-4o-mini"
    }
}
```
Make sure you put the correct value for your Github personal access token.

### 4. Create Project Structure
```bash
mkdir Models
```

### 5. Implement Core Files

**Models/ConcurrentAggregationExecutor.cs** - Response aggregation

```C#
using System;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ConcurrentWorkflow.Models;

/// <summary>
/// Executor that aggregates the results from the concurrent agents.
/// </summary>
internal sealed class ConcurrentAggregationExecutor(int expectedAgentCount = 3) :
    Executor<List<ChatMessage>>("ConcurrentAggregationExecutor") {
    private readonly List<ChatMessage> _messages = [];
    private readonly int _expectedCount = expectedAgentCount;

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

        if (this._messages.Count == _expectedCount) {
            var formattedMessages = string.Join(Environment.NewLine,
                this._messages.Select(m => $"{m.AuthorName}: {m.Text}"));
            await context.YieldOutputAsync(formattedMessages, cancellationToken);
        }
    }
}
```

**Models/ConcurrentStartExecutor.cs** - Workflow initiation

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
    public override async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Broadcast the message to all connected agents. Receiving agents will queue
        // the message but will not start processing until they receive a turn token.
        await context.SendMessageAsync(new ChatMessage(ChatRole.User, message), cancellationToken);

        // Broadcast the turn token to kick off the agents.
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken);
    }
}
```

**Program.cs** - Main workflow orchestration

```C#
// Import necessary namespaces for Azure, AI agents, workflows, configuration, and OpenAI
using Azure;
using ConcurrentWorkflow.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

// Build configuration from appsettings.json
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();

// Retrieve configuration values for GitHub OpenAI integration
string? apiKey = config["GitHub:Token"];
string? model = config["GitHub:Model"] ?? "openai/gpt-4o-mini";
string? endpoint = config["GitHub:ApiEndpoint"] ?? "https://models.github.ai/inference";

// Log the configuration for debugging
Console.WriteLine($"Using model: {model} at endpoint: {endpoint} with API key: {(apiKey != null ? "****" : "null")}");

// Create a chat client for OpenAI
IChatClient chatClient = new ChatClient(
    model,
    new AzureKeyCredential(apiKey!),
    new OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint)
    }
)
.AsIChatClient();

// Create AI agents with specialized expertise
ChatClientAgent physicist = new(
    chatClient,
    name: "Physicist",
    instructions: "You are an expert in physics. You answer questions from a physics perspective."
);

ChatClientAgent chemist = new(
    chatClient,
    name: "Chemist",
    instructions: "You are an expert in chemistry. You answer questions from a chemistry perspective."
);

// Create executors for workflow management
var startExecutor = new ConcurrentStartExecutor();
var aggregationExecutor = new ConcurrentAggregationExecutor(2);

// Build the workflow by adding executors and connecting them
var workflow = new WorkflowBuilder(startExecutor)
    .AddFanOutEdge(startExecutor, targets: [physicist, chemist])
    .AddFanInEdge(sources: [physicist, chemist], aggregationExecutor)
    .WithOutputFrom(aggregationExecutor)
    .Build();

// Execute the workflow in streaming mode
await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, "What is temperature?");
Console.WriteLine($"Workflow execution started.");

// Watch the workflow events and handle outputs
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is WorkflowOutputEvent output)
    {
        Console.WriteLine($"Workflow completed with results:\n{output.Data}");
    }
}

```

### 6. Run the application

```bash
dotnet run
```

**Expected Output**
```
Using model: openai/gpt-4o-mini at endpoint: https://models.github.ai/inference with API key: ****
Workflow completed with results:
Chemist: Temperature is a measure of the average kinetic energy of the particles in a substance. It indicates how hot or cold an object is and is a fundamental physical quantity in various scientific fields, including chemistry, physics, and thermodynamics.

In more detail, temperature reflects the energy of motion of atoms and molecules. When the temperature increases, the average speed of the particles increases, resulting in higher kinetic energy. Conversely, lower temperatures correspond to slower-moving particles and lower kinetic energy.

Temperature is measured using different scales, with the most common being Celsius (°C), Fahrenheit (°F), and Kelvin (K). The Kelvin scale is particularly important in scientific contexts because it is an absolute temperature scale, starting at absolute zero (0 K), the point at which all classical molecular motion ceases, which corresponds to -273.15 °C.

In chemistry, temperature plays a crucial role in influencing reaction rates, the solubility of substances, and other thermodynamic properties, making it an essential factor in both experimental and theoretical studies.
Physicist: Temperature is a measure of the average kinetic energy of the particles in a substance. It is a fundamental parameter in thermodynamics and plays a critical role in determining the state of matter (solid, liquid, gas) and the direction of heat flow between objects.

In more technical terms, temperature can be defined in several ways:

1. Thermodynamic Definition: In thermodynamics, temperature is a measure of the thermal energy of a system in relation to its entropy. It can be defined through the second law of thermodynamics, which states that heat will flow spontaneously from a hotter object to a cooler one.

2. Kinetic Theory: According to the kinetic theory of gases, temperature is directly related to the average kinetic energy of the particles in a gas. As the temperature increases, the average speed of the particles increases, leading to higher kinetic energy.

3. Temperature Scales: Temperature is measured using various scales, with the most common being Celsius (°C), Kelvin (K), and Fahrenheit (°F). The Kelvin scale is the SI unit for temperature, where 0 K corresponds to absolute zero, the point at which molecular motion theoretically comes to a complete halt.

4. Absolute Zero: Absolute zero (0 K or -273.15 °C) is the theoretical temperature at which a system possesses minimal thermal energy. At this point, the entropy of a perfect crystal reaches its minimum value.

In practical applications, temperature is crucial in a wide range of fields, including meteorology, engineering, biology, and materials science, influencing everything from weather patterns to the behavior of materials under different conditions.
```