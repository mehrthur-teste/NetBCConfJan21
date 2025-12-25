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
string endpoint = builder.Configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is missing");
string apiKey = builder.Configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey configuration is missing");
string deploymentName = builder.Configuration["AzureOpenAI:DeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName configuration is missing");
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

    AIAgent agent = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey))
                .GetChatClient(deploymentName)
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


app.MapPost("/agent-run", async (HttpContext context) => {

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


    AIAgent agent = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey))
                .GetChatClient(deploymentName)
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
