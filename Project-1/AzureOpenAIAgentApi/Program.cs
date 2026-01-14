using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);


// Adicionando a string de conexão no Configuration
builder.Services.AddSingleton(sp =>
    builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty
);

// Registrando o repositório de Employees para injeção de dependência
builder.Services.AddScoped<EmployeeRepository>();

// Read configuration values for Azure OpenAI
string endpoint = builder.Configuration["GitHub:Endpoint"] ?? throw new InvalidOperationException("GitHub:Endpoint configuration is missing");
string apiKey = builder.Configuration["GitHub:ApiKey"] ?? throw new InvalidOperationException("GitHub:ApiKey configuration is missing");
string model = builder.Configuration["GitHub:model"] ?? throw new InvalidOperationException("GitHub:model configuration is missing");
var app = builder.Build();


app.MapGet("health-check/", () => "Hello World!");

// Endpoint para pegar empregados com o filtro
app.MapPatch("/employees", async (EmployeeRepository repository, HttpContext context) =>
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
