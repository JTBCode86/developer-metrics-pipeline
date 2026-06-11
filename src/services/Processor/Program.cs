using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Extensions.NETCore.Setup;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Processor.Internal.Domain;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// --- Configuração da Injeção de Dependência ---
// Garante que o AWS SDK utilize as configs do appsettings.json ou variáveis de ambiente
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddAWSService<IAmazonDynamoDB>();

// Adiciona explorador de endpoints (Obrigatório para Swagger reconhecer as rotas)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Metrics Processor API", Version = "v1" });
});

var app = builder.Build();

// --- Middleware ---
// Swagger sempre disponível em desenvolvimento
app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Metrics Processor API V1");
    c.RoutePrefix = string.Empty; // Swagger na raiz
});

// --- Endpoints ---
// 1. GET /health - Verifica conexões (Health Check)
app.MapGet("/health", async ([FromServices] IAmazonSQS sqs, [FromServices] IAmazonDynamoDB db) =>
{
    try
    {
        await sqs.ListQueuesAsync(new ListQueuesRequest());
        await db.DescribeTableAsync(new DescribeTableRequest { TableName = "MetricsTable" });
        return Results.Ok(new { status = "UP" });
    }
    catch
    {
        return Results.Json(new { status = "DOWN" }, statusCode: 503);
    }
});

// 2. POST /api/eventos - Recebe eventos e publica no SQS
app.MapPost("/api/eventos", async (RawEvent evento, IAmazonSQS sqsClient, IConfiguration configuration) =>
{
    var queueUrl = configuration["AWS:QueueUrl"];

    var request = new SendMessageRequest
    {
        QueueUrl = queueUrl,
        MessageBody = JsonSerializer.Serialize(evento)
    };

    // Publica no SQS
    await sqsClient.SendMessageAsync(request);

    return Results.Accepted(); 
});

// 3. GET /metrics/{developer_id} - Retorna todos os eventos do desenvolvedor
app.MapGet("/metrics/{developer_id}", async (string developer_id,[FromServices] IAmazonDynamoDB dbClient,[FromServices] IConfiguration config) =>
{
    // Forçamos um valor padrão caso a configuração falhe
    var tableName = "developer_summary";

    if (string.IsNullOrWhiteSpace(tableName))
        return Results.BadRequest(new { message = "Configuração 'AWS:TableName' não encontrada." });

    var request = new QueryRequest
    {
        TableName = tableName,
        // Removeremos o IndexName para testar a busca simples primeiro
        KeyConditionExpression = "developer_id = :devId",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
            { ":devId", new AttributeValue { S = developer_id } }
        }
    };

    try
    {
        var response = await dbClient.QueryAsync(request);

        // Transformação simples para o formato limpo
        var results = response.Items.Select(item => new {
            developer_id = item["developer_id"].S,
            total_commits = item.ContainsKey("total_commits") ? item["total_commits"].N : "0"
            // Adicione aqui os outros campos que você deseja retornar
        });

        return Results.Ok(results);
    }
    catch (AmazonDynamoDBException ex)
    {
        // Isso vai nos dizer exatamente se o problema é o nome da tabela ou o índice
        return Results.Json(new { message = ex.Message }, statusCode: 500);
    }

});

// 4. GET: /metrics/{developer_id}/summary (Agregado)
app.MapGet("/api/metrics/{developer_id}/summary", async (string developer_id, [FromServices] IAmazonDynamoDB db, IConfiguration config) =>
{
    var request = new GetItemRequest
    {
        TableName = "developer_summary",
        Key = new Dictionary<string, AttributeValue> { { "developer_id", new AttributeValue { S = developer_id } } }
    };

    var response = await db.GetItemAsync(request);

    if (!response.IsItemSet) return Results.NotFound(new { message = "Desenvolvedor não encontrado." });

    // Extração dos atributos do DynamoDB
    var item = response.Item;

    string GetVal(string key) => item.TryGetValue(key, out var val) ? val.N : "0";

    // Extração segura
    int totalCommits = int.Parse(GetVal("total_commits"));
    int totalPRs = int.Parse(GetVal("total_pull_requests"));
    int eventsProcessed = int.Parse(GetVal("events_processed"));
    double totalReviewTimeSum = double.Parse(GetVal("total_review_time_sum"));
    double avgReviewTime = double.Parse(GetVal("avg_review_time_minutes"));
    string lastActivity = item.ContainsKey("last_activity") ? item["last_activity"].S : DateTime.MinValue.ToString("o");

    return Results.Ok(new
    {
        developer_id = developer_id,
        total_commits = totalCommits,
        total_pull_requests = totalPRs,
        avg_review_time_minutes = avgReviewTime,
        events_processed = eventsProcessed,
        last_activity = lastActivity
    });
})

.WithName("BuscarEventoPorId");

app.Run();