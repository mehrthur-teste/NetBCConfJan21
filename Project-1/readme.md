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


In _Program.cs_, add this code right under _"var builder = WebApplication.CreateBuilder(args);"_ to read-in the settings from _appsettings.Development.json_, register the _OpenApi_ service, and configure DB connection:
```c#
string? apiKey = builder.Configuration["GitHub:Token"];
string? model = builder.Configuration["GitHub:Model"] ?? "openai/gpt-4o-mini";
string? endpoint = builder.Configuration["GitHub:ApiEndpoint"] ?? "https://models.github.ai/inference";

builder.Services.AddSingleton(sp =>
    builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty
);
```

Add another folder named _Dapper_ and add to it this C# class named _EmployeeRepository.cs_:
```csharp
public class EmployeeRepository {
    private readonly string _connectionString;

    public EmployeeRepository(string connectionString) {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<PersonEntity>> GetEmployeesAsync(string filter) {
        try
        {
            using (IDbConnection dbConnection = new NpgsqlConnection(_connectionString)) {
                dbConnection.Open();
                var employees = await dbConnection.QueryAsync<PersonEntity>(filter);
                return employees;
            } 
        } catch (Exception ex) {
            using (IDbConnection dbConnection = new SqliteConnection(_connectionString))
            {
                dbConnection.Open();
                var employees = await dbConnection.QueryAsync<PersonEntity>(filter);
                return employees;
            }
        }
    }
}
```

Register _EmployeeRepository_ by adding this code to _Program.cs_:
```c#
builder.Services.AddScoped<EmployeeRepository>();
```
Remove all Weather related code in _Program.cs_. 

Add these endpoints right before _app.Run();_ in _Program.cs_:

```C#
app.MapGet("health-check/", () => "Hello World!");

// Endpoint para pegar empregados com o filtro
app.MapPatch("/sql-agent-run", async (EmployeeRepository repository, HttpContext context) =>
{
    var chatConfig = await context.Request.ReadFromJsonAsync<ChatConfig>();
    JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(AIResponse));

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
    var filter = response.Deserialize<AIResponse>(JsonSerializerOptions.Web);

    var employees = await repository.GetEmployeesAsync(filter.AIAnswer!);

    return Results.Ok(employees);
});


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

    Console.WriteLine(response.ToString());

    var personInfo = response.Deserialize<PersonInfo>(JsonSerializerOptions.Web);
    return Results.Ok(personInfo);
});
```
Add a folder named _Database_ and add to it a file named _init.sql_ with this SQL content:

```sql
CREATE TABLE IF NOT EXISTS employee (
    Name TEXT,
    Age INTEGER,
    Occupation TEXT
);

INSERT INTO employee (Name, Age, Occupation) VALUES
('John Doe', 30, 'Software Engineer'),
('Jane Smith', 28, 'Data Analyst'),
('Alice Johnson', 35, 'Product Manager');
```
Using an SQLite interface, execute the above SQL command to create an _employees_ table and populate it with some sample data.

You can now start your application by executing the following command in the termnal window:
```bash
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
curl -X PATCH http://localhost:5122/sql-agent-run \
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
