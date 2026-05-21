using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Builder;
using Processor.Internal.Domain;
using System.Text.Json;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Registra o cliente SQS (o LocalStack vai injetar a URL via config)
builder.Services.AddAWSService<IAmazonSQS>();

var app = builder.Build();

app.MapPost("/api/eventos", async (RawEvent rawEvent, IAmazonSQS sqsClient, IConfiguration config) =>
{
    // 1. Validação de Domínio (a sua classe já faz isso!)
    var (isValid, reason) = rawEvent.Validate();
    if (!isValid)
        return Results.BadRequest(new { error = reason });

    // 2. Envio para a Fila raw-events
    var queueUrl = config["AWS:QueueUrl"]; // Defina isso no appsettings.json
    var messageBody = JsonSerializer.Serialize(rawEvent);

    await sqsClient.SendMessageAsync(new SendMessageRequest
    {
        QueueUrl = queueUrl,
        MessageBody = messageBody
    });

    return Results.Accepted();
});

app.Run();