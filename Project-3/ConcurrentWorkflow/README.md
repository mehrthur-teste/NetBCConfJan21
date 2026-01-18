# ConcurrentWorkflow

ConcurrentWorkflow demonstrates concurrent AI agent processing using Microsoft Agents AI Workflows with GitHub Models API.

## 📋 Índice
- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Setup Instructions](#setup-instructions)
- [Usage Examples](#usage-examples)
- [Project Structure](#project-structure)
- [Creation of the project](#creation-of-the-project)

# ConcurrentWorkflow Implementation
![alt text](../../p2.png)

<details>
<summary>🎯 Overview</summary>

This project demonstrates concurrent workflow patterns using specialized AI agents (Physicist and Chemist) that process questions simultaneously and aggregate their responses. Built with Microsoft Agents AI Workflows and GitHub Models API.
</details>

## How it Works

The workflow creates two specialized AI agents that process the same question concurrently:

| Agent | Specialization | Role |
|-------|---------------|------|
| **Physicist** | Physics expertise | Answers from physics perspective |
| **Chemist** | Chemistry expertise | Answers from chemistry perspective |

### Workflow Process

1. **Start Executor** - Receives user question and broadcasts to agents
2. **Fan-Out** - Question sent simultaneously to both agents
3. **Concurrent Processing** - Both agents process independently
4. **Fan-In** - Responses collected by aggregation executor
5. **Output** - Combined formatted response returned

## Prerequisites
- .NET 10.0 SDK or later
- GitHub Personal Access Token with Models access
- Visual Studio or VS Code

## Setup Instructions

<details>
<summary>🔧 Configuration</summary>

1. Get GitHub Personal Access Token:
   - Go to GitHub Settings > Developer settings > Personal access tokens
   - Generate token with Models access

2. Configure `appsettings.json`:
   ```json
   {
       "GitHub": {
           "Token": "your-github-token-here",
           "ApiEndpoint": "https://models.github.ai/inference",
           "Model": "openai/gpt-4o-mini"
       }
   }
   ```

3. Test token (optional):
   ```bash
   chmod +x test-token.sh
   ./test-token.sh
   ```
</details>

<details>
<summary>🚀 Running the Application</summary>

1. Restore dependencies:
   ```bash
   dotnet restore
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run
   ```
</details>

## Usage Examples

<details>
<summary>🔍 Sample Questions</summary>

**Question:** "What is temperature?"

**Expected Output:**
```
Physicist: Temperature is a measure of the average kinetic energy of particles in a system...
Chemist: Temperature affects reaction rates and chemical equilibrium according to Le Chatelier's principle...
```

**Other Examples:**
- "What is energy?"
- "Explain atomic structure"
- "How do chemical bonds work?"
- "What is entropy?"
</details>

## 📝 Response Format

The application outputs responses in the format:
```
Workflow execution started.
Workflow completed with results:
Physicist: [Physics perspective answer]
Chemist: [Chemistry perspective answer]
```

## Project Structure

```
ConcurrentWorkflow/
├── Program.cs                          # Main application entry point
├── Models/
│   ├── ConcurrentStartExecutor.cs      # Workflow start executor
│   └── ConcurrentAggregationExecutor.cs # Response aggregation executor
├── appsettings.json                    # Configuration file
├── test-token.sh                       # Token validation script
└── ConcurrentWorkflow.csproj          # Project dependencies
```

### Key Components

<details>
<summary>📁 Models Folder</summary>

**ConcurrentStartExecutor.cs**
```csharp
internal sealed class ConcurrentStartExecutor() : Executor<string>("ConcurrentStartExecutor")
{
    public override async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Broadcast message to all connected agents
        await context.SendMessageAsync(new ChatMessage(ChatRole.User, message), cancellationToken);
        // Send turn token to start processing
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken);
    }
}
```

**ConcurrentAggregationExecutor.cs**
```csharp
internal sealed class ConcurrentAggregationExecutor() : Executor<List<ChatMessage>>("ConcurrentAggregationExecutor")
{
    public override async ValueTask HandleAsync(List<ChatMessage> message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        _messages.AddRange(message);
        if (_messages.Count == 2) // Wait for both agents
        {
            var formattedMessages = string.Join(Environment.NewLine,
                _messages.Select(m => $"{m.AuthorName}: {m.Text}"));
            await context.YieldOutputAsync(formattedMessages, cancellationToken);
        }
    }
}
```
</details>

## Creation of the project

<details>
<summary>🛠️ Step-by-Step Creation</summary>

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


### 7. Configure GitHub Models
- Obtain GitHub Personal Access Token
- Configure appsettings.json with token and model settings
- Test connection with provided script
</details>

## Key Features

- **Concurrent Processing**: Multiple AI agents process simultaneously
- **Specialized Agents**: Physics and Chemistry domain experts
- **Workflow Orchestration**: Microsoft Agents AI Workflows framework
- **GitHub Models Integration**: Uses GitHub's AI model inference API
- **Streaming Output**: Real-time workflow execution monitoring
- **Configurable Models**: Easy switching between different AI models

## Troubleshooting

<details>
<summary>🔧 Common Issues</summary>

**Token Issues:**
- Ensure GitHub token has Models access
- Check token is correctly set in appsettings.json
- Run test-token.sh to validate

**Build Issues:**
- Ensure .NET 10.0 SDK is installed
- Run `dotnet restore` before building
- Check all package versions are compatible

**Runtime Issues:**
- Verify internet connection for GitHub Models API
- Check API endpoint is accessible
- Ensure model name is correct in configuration
</details>