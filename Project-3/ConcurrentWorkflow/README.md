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

### 3. Create Project Structure
```bash
mkdir Models
```

### 4. Implement Core Files

**Program.cs** - Main workflow orchestration
**Models/ConcurrentStartExecutor.cs** - Workflow initiation
**Models/ConcurrentAggregationExecutor.cs** - Response aggregation
**appsettings.json** - GitHub Models configuration

### 5. Configure GitHub Models
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