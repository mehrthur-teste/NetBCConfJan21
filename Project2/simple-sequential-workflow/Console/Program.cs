using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;

/*
This tutorial demonstrates how to create a simple sequential workflow using Agent Framework Workflows.
*/

/// <summary>
/// First executor: converts input text to uppercase.
/// </summary>
Func<string, string> uppercaseFunc = s => s.ToUpperInvariant();
var uppercase = uppercaseFunc.BindAsExecutor("UppercaseExecutor");
ReverseTextExecutor reverse = new();

// Build the workflow by connecting executors sequentially
WorkflowBuilder builder = new(uppercase);
builder.AddEdge(uppercase, reverse).WithOutputFrom(reverse);
var workflow = builder.Build();

// Execute the workflow with input data
await using Run run = await InProcessExecution.RunAsync(workflow, "Hello, World!");
foreach (WorkflowEvent evt in run.NewEvents)
{
    switch (evt)
    {
        case ExecutorCompletedEvent executorComplete:
            Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
            break;
    }
}
/// <summary>
/// Second executor: reverses the input text and completes the workflow.
/// </summary>
internal sealed class ReverseTextExecutor() : Executor<string, string>("ReverseTextExecutor")
{
    public override ValueTask<string> HandleAsync(string input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Reverse the input text
        return ValueTask.FromResult(new string(input.Reverse().ToArray()));
    }
}



