using Aggregator;
using Aggregator.Internal.Domain;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel; // Necessário para ScanOperator e IDynamoDBContext
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Extensions.NETCore.Setup; // Necessário para AddAWSService
using Amazon.SQS;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 1. Registro de Serviços da AWS (LocalStack)
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddScoped<IDynamoDBContext, DynamoDBContext>();
builder.Services.AddAWSService<IAmazonSQS>();

// 2. Registro do Worker que consome o SQS (seu código existente)
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// 3. API REST para consulta (Requisito do Case)
// GET /metrics/{developerId}
app.MapGet("/metrics/{developerId}", async (string developerId, IDynamoDBContext dbContext) =>
{
    // Exemplo de como consultar no DynamoDB usando o SDK
    var conditions = new List<ScanCondition>
    {
        //new ScanCondition("DeveloperId", Amazon.DynamoDBv2.DataModel.ScanOperator.Equal, developerId)
        new ScanCondition("DeveloperId", ScanOperator.Equal, developerId)
    };

    var events = await dbContext.ScanAsync<ProcessedEvent>(conditions).GetRemainingAsync();
    return Results.Ok(events);
});

// GET /metrics/{developerId}/summary
app.MapGet("/metrics/{developerId}/summary", async (string developerId, IDynamoDBContext dbContext) =>
{
    // Busca o resumo na tabela developer_summary
    var summary = await dbContext.LoadAsync<DeveloperSummary>(developerId);

    return summary != null
        ? Results.Ok(summary)
        : Results.NotFound(new { message = "Resumo não encontrado para este desenvolvedor." });
});

// GET /health
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();