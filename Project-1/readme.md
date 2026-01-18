Before all, ensure that the you have .net 10 (LTS version) and SLQ Lite

Create a new .NET _WebApi_ project by typing the following terminal commands in a suitable working directory:
```bash
dotnet new webapi -n GitHubAIAgentAPI --framework net10.0
cd GitHubAIAgentAPI
dotnet new gitignore
```

If you dont have SQL Lite and you notebook is mac :
  1. Inside folder `GitHubAIAgentAPI` create another folder and give this name  - `sql-lite`.
  2. Then create a `.sh` file which the recomended name is **`setup_sqlite.sh`** .
  3. Inside this file paste below code :
    ```bash
    !/bin/bash

    set -e

    echo "🔧 Installing SQLite..."

    # Detect OS
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        sudo apt update
        sudo apt install -y sqlite3
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        if ! command -v brew &> /dev/null; then
            echo "❌ Homebrew not found. Install Homebrew first."
            exit 1
        fi
        brew install sqlite
    else
        echo "❌ Unsupported OS"
        exit 1
    fi

    echo "📁 Creating database folder..."
    mkdir -p database

    echo "📝 Creating init.sql..."
    cat <<EOF > database/init.sql
    CREATE TABLE IF NOT EXISTS employee (
        Name TEXT,
        Age INTEGER,
        Occupation TEXT
    );

    INSERT INTO employee (Name, Age, Occupation) VALUES
    ('John Doe', 30, 'Software Engineer'),
    ('Jane Smith', 28, 'Data Analyst'),
    ('Alice Johnson', 35, 'Product Manager');
    EOF

    echo "🗄️ Creating SQLite database and running init.sql..."
    sqlite3 database/app.db < database/init.sql

    echo "✅ SQLite setup completed!"
    echo "📍 Database path: database/app.db"

    brew install sqlite
    
 4. Save the file and give execute permission by running this command in terminal :
    ```bash
    chmod +x setup_sqlite.sh
    ```
 5. Now run the script by executing this command in terminal :
    ```bash
    ./setup_sqlite.sh
    ```
In the same terminal window (inside GitHubAIAgentAPI workspace), add required packages with:
```bash
dotnet add package Azure.AI.OpenAI -v 2.7.0-beta.2
dotnet add package Azure.Identity -v 1.17.1
dotnet add package Dapper -v 2.1.66
dotnet add package Microsoft.Agents.AI.OpenAI -v 1.0.0-preview.251204.1
dotnet add package Microsoft.Data.Sqlite
dotnet add package Npgsql
```
Open the folder in your favorite editor and update the _appsettings.Development.json_ file with these sections. 

```json
"GitHub": {
    "Token": "PUT YOUR GITHUB PERSONAL ACCESS TOKEN HERE",
    "ApiEndpoint": "https://models.github.ai/inference",
    "ApiKey": "",
    "Model": "openai/gpt-4o-mini"
},
"ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db;"
}
```
Remember to:
1. replace _PUT YOUR GITHUB PERSONAL ACCESS TOKEN HERE_ with your GitHub personal access token.
2. Add _appsettings.Development.json_ to your .gitignore so that this confidential info does not find its way into source control.

Create a folder named _Entities_ and add to it a C# class file named _PersonEntity.cs_ with this code:
```C#
public class PersonEntity {
    public string? Name { get; set; }
    public int? Age { get; set; }
    public string? Occupation { get; set; }
}
```

Create a _Models_ folder and add to it C# classes _AIResponse.cs_, _ChatConfig.cs_, and _PersonInfo.cs_ :

**AIResponse.cs**
```C#
public class AIResponse {
    public string? AIAnswer { get; set; }
}
```

**ChatConfig.cs**
```C#
public class ChatConfig {
    public string? Prompt1 { get; set; }
    public string? Prompt2 { get; set; }
    public string? NameAssistant { get; set; }
    public string? Description { get; set; }
    public string? SchemaName { get; set; }
    public string? SchemaDescription { get; set; }
    public string? Intructions { get; set; }
    public string? Go { get; set; }
}
```

**PersonInfo.cs**
```C#
public class PersonInfo {
    public string? Name { get; set; }
    public int? Age { get; set; }
    public string? Occupation { get; set; }
    public string? AIresponse { get; set; }
}
```

Add another folder named _Dapper_ and add to it this C# class named _EmployeeRepository.cs_:
```csharp
using Dapper;
using Npgsql;  // Namespace do PostgreSQL
using System.Data;
using Microsoft.Data.Sqlite; // para SQLite

public class EmployeeRepository
{
    private readonly string _connectionString;

    public EmployeeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // connect to SQLite, if fails connect to PostgreSQL
    public async Task<IEnumerable<PersonEntity>> GetEmployeesAsync(string filter)
    {
        try
        {
            using (IDbConnection dbConnection = new SqliteConnection(_connectionString))
            {
                dbConnection.Open();
                var employees = await dbConnection.QueryAsync<PersonEntity>(filter);
                return employees;
            } 
        }
        catch (Exception ex)
        {
            using (IDbConnection dbConnection = new NpgsqlConnection(_connectionString))
            {
                dbConnection.Open();
                var employees = await dbConnection.QueryAsync<PersonEntity>(filter);
                return employees;
            }
        }
    }
}

```


In _Program.cs_, add this code right under _"var builder = WebApplication.CreateBuilder(args);"_ to read-in the settings from _appsettings.Development.json_, register the _OpenApi_ service, and configure DB connection:
```C#
// Import necessary namespaces for Azure, AI agents, JSON handling, and OpenAI
using Azure;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;
using OpenAI;

// Create a new web application builder with command-line arguments
var builder = WebApplication.CreateBuilder(args);


// Register the connection string for database access
builder.Services.AddSingleton(sp =>
    builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty
);

// Register the EmployeeRepository for dependency injection
builder.Services.AddScoped<EmployeeRepository>();

// Read configuration values for Azure OpenAI
string endpoint = builder.Configuration["GitHub:Endpoint"] ?? throw new InvalidOperationException("GitHub:Endpoint configuration is missing");
string apiKey = builder.Configuration["GitHub:ApiKey"] ?? throw new InvalidOperationException("GitHub:ApiKey configuration is missing");
string model = builder.Configuration["GitHub:model"] ?? throw new InvalidOperationException("GitHub:model configuration is missing");
var app = builder.Build();

// Define a simple health check endpoint
app.MapGet("/", () => "Is Running!");

// Endpoint to retrieve employees based on AI-generated filter
app.MapPatch("/employees", async (EmployeeRepository repository, HttpContext context) =>
{
    // Read the chat configuration from the request body
    var chatConfig = await context.Request.ReadFromJsonAsync<ChatConfig>();
    
    // Create a JSON schema for the AI response
    JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(AIResponse));

    // Configure chat options with instructions and response format
    ChatOptions chatOptions = new()
    {
        Instructions = chatConfig!.Intructions,
        ResponseFormat = ChatResponseFormat.ForJsonSchema(
            schema: schema,
            schemaName: chatConfig.SchemaName,
            schemaDescription: chatConfig.SchemaDescription)
    };

    // Create a chat client for OpenAI
    IChatClient chatClient = new OpenAI.Chat.ChatClient(
        model,
        new AzureKeyCredential(apiKey!),
        new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint)
        }
    )
    .AsIChatClient();

    // Create an AI agent using the chat client
    var agent = chatClient
    .CreateAIAgent(new ChatClientAgentOptions()
    {
        Name = chatConfig.NameAssistant,
        Description = chatConfig.Description,
        ChatOptions = chatOptions
    });

    // Build the dynamic message by replacing placeholders
    string? dynamicMessage = chatConfig!.Go!.Replace("{chatConfig.Prompt1}", chatConfig.Prompt1)
                                            .Replace("{chatConfig.Prompt2}", chatConfig.Prompt2);
    
    // Run the AI agent with the dynamic message
    var response = await agent.RunAsync(dynamicMessage);
    
    // Deserialize the response to get the filter
    var filter = response.Deserialize<AIResponse>(JsonSerializerOptions.Web);

    // Query the database for employees using the AI-generated filter
    var employees = await repository.GetEmployeesAsync(filter.AIAnswer!);

    // Return the list of employees
    return Results.Ok(employees);
});

// Endpoint to run AI agent for generating person information
app.MapPost("/agent-run", async (HttpContext context) =>
{

    var chatConfig = await context.Request.ReadFromJsonAsync<ChatConfig>();

    JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(PersonInfo));

    ChatOptions chatOptions = new()
    {
        Instructions = chatConfig!.Intructions,
        ResponseFormat = ChatResponseFormat.ForJsonSchema(
            schema: schema,
            schemaName: chatConfig.SchemaName,
            schemaDescription: chatConfig.SchemaDescription)
    };

    IChatClient chatClient = new OpenAI.Chat.ChatClient(
        model,
        new AzureKeyCredential(apiKey!),
        new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint)
        }
    )
    .AsIChatClient();

    var agent = chatClient
    .CreateAIAgent(new ChatClientAgentOptions()
    {
        Name = chatConfig.NameAssistant,
        Description = chatConfig.Description,
        ChatOptions = chatOptions
    });

    string? dynamicMessage = chatConfig!.Go!.Replace("{chatConfig.Prompt1}", chatConfig.Prompt1)
                                            .Replace("{chatConfig.Prompt2}", chatConfig.Prompt2);
    var response = await agent.RunAsync(dynamicMessage);
    var personInfo = response.Deserialize<PersonInfo>(JsonSerializerOptions.Web);
    return Results.Ok(personInfo);
});

app.Run();

```

You can now start your application by executing the following command in the termnal window:
```bash
dotnet clean
dotnet build
dotnet run
```

To text out the _/agent-run_ endpoint, open another terminal window and run the curl command:
```bash
curl -X POST http://localhost:5241/agent-run \
-H "Content-Type: application/json" \
-d '{
    "Prompt1": "Cristiano Ronaldo",
    "Prompt2": "Software Engineer",
    "NameAssistant": "HelpfulAssistant",
    "Description" : "An AI assistant that provides structured information about people.",
    "schemaName": "PersonInfo",
    "schemaDescription": "Information about a person including their name, age, and occupation",
    "Intructions" : "You are a helpful assistant that provides structured information about people.",
    "Go" : "Please provide information about {chatConfig.Prompt1} see on the internet, is he {chatConfig.Prompt2} or not?"
}'
```

**NOTE: Adjust the port number in the above command to match your environment.**

Similaraly, you can execute this command to test out the _sql-agent-run_ endpoint:
```bash
curl -X PATCH http://localhost:5122/employees \
-H "Content-Type: application/json" \
-d '{
    "Prompt1": "Table is employee built as Name VARCHAR(100), Age INT, Occupation VARCHAR(100)",
    "NameAssistant": "SQLHelpfulAssistant",
    "Description" : "An AI assistant that query to inject sql postgres client.",
    "schemaName": "PersonInfo",
    "schemaDescription": "Contains only a raw SQL query with no explanation or additional text",
    "Go" : "Please provide a quickly and sample query -  select all employe who is not software enginner remember that - {chatConfig.Prompt1} ?"
}'
```

Or running using .http
```http
@GitHubAIAgentAPI_HostAddress = http://localhost:5010


POST {{GitHubAIAgentAPI_HostAddress}}/agent-run
Content-Type: application/json

{
    "Prompt1": "Cristiano Ronaldo",
    "Prompt2": "Software Engineer",
    "NameAssistant": "HelpfulAssistant",
    "Description": "An AI assistant that provides structured information about people.",
    "schemaName": "PersonInfo",
    "schemaDescription": "Information about a person including their name, age, and occupation",
    "Instructions": "You are a helpful assistant that provides structured information about people.",
    "Go": "Please provide information about {chatConfig.Prompt1} see on the internet, is he {chatConfig.Prompt2} or not?"
}

###

PATCH {{GitHubAIAgentAPI_HostAddress}}/employees
Content-Type: application/json

{
    "Prompt1": "Table is employee built as Name VARCHAR(100), Age INT, Occupation VARCHAR(100)",
    "NameAssistant": "SQLHelpfulAssistant",
    "Description": "An AI assistant that query to inject sql postgres client.",
    "schemaName": "PersonInfo",
    "schemaDescription": "Contains only a raw SQL query with no explanation or additional text",
    "Go": "Please provide a quickly and sample query -  select all employe who is not software enginner remember that - {chatConfig.Prompt1} ?"
}
```
