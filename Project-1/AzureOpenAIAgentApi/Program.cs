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
app.MapGet("health-check/", () => "Hello World!");

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


    // AIAgent agent = new AzureOpenAIClient(
    //         new Uri(endpoint),
    //         new AzureKeyCredential(apiKey))
    //             .GetChatClient(model)
    //             .CreateAIAgent(new ChatClientAgentOptions()
    //             {
    //                 Name = chatConfig.NameAssistant,
    //                 Description = chatConfig.Description,
    //                 ChatOptions = chatOptions
    //             });

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
