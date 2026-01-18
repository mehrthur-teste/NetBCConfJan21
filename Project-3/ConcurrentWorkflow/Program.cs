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
