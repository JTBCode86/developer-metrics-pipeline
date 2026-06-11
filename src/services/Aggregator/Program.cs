using Aggregator;
using Aggregator.Internal.Domain;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel; 
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Extensions.NETCore.Setup;
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

// GET /metrics/{developerId}
app.MapGet("/metrics/{developerId}", async (string developerId, IDynamoDBContext dbContext) =>
{
    var conditions = new List<ScanCondition>
    {
        new ScanCondition("DeveloperId", ScanOperator.Equal, developerId)
    };

    var events = await dbContext.ScanAsync<ProcessedEvent>(conditions).GetRemainingAsync();
    return Results.Ok(events);
});

// GET /metrics/{developerId}/summary
app.MapGet("/metrics/{developerId}/summary", async (string developerId, IDynamoDBContext dbContext) =>
{
    var summary = await dbContext.LoadAsync<DeveloperSummary>(developerId);

    if (summary == null)
        return Results.NotFound(new { message = "Resumo não encontrado." });

    // Cálculo da média na hora da leitura
    double avgReviewTime = summary.EventsProcessed > 0
        ? summary.TotalReviewTimeSum / summary.EventsProcessed
        : 0;

    // Retorno customizado (Polimento)
    return Results.Ok(new
    {
        developer_id = summary.DeveloperId,
        total_commits = summary.TotalCommits,
        total_pull_requests = summary.TotalPullRequests,
        avg_review_time_minutes = Math.Round(avgReviewTime, 2),
        last_activity = summary.LastActivity
    });
});

// GET /health
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();