using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using Aggregator;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração de Logs Estruturados em JSON para o Aggregator
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ ";
});

var configuration = builder.Configuration;

// 1. Configuração e Injeção do cliente SQS
builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    var sqsConfig = new AmazonSQSConfig
    {
        ServiceURL = configuration["AWS:ServiceURL"] ?? "http://localhost:4566",
        AuthenticationRegion = configuration["AWS:Region"] ?? "us-east-1"
    };
    // Garante que passa a classe de configuração correta para o construtor correspondente
    return new AmazonSQSClient(sqsConfig);
});

// 2. Configuração e Injeção do cliente DynamoDB
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var dynamoConfig = new AmazonDynamoDBConfig
    {
        ServiceURL = configuration["AWS:ServiceURL"] ?? "http://localhost:4566",
        AuthenticationRegion = configuration["AWS:Region"] ?? "us-east-1"
    };
    // Corrigido de 'AmazonAmazonDynamoDBClient' para a classe oficial 'AmazonDynamoDBClient'
    return new AmazonDynamoDBClient(dynamoConfig);
});

// Ativa o Worker em background para consumir a fila SQS
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// ==========================================
// 3. Definição das Rotas HTTP (API REST)
// ==========================================

// Endpoint de Health Check
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

// Endpoint 1: Obter histórico de eventos brutos de um desenvolvedor
app.MapGet("/metrics/{developer_id}", async (string developer_id, IAmazonDynamoDB dynamoDb) =>
{
    try
    {
        // Utiliza Scan com Filtro simplificado para ambiente de teste local
        var request = new ScanRequest
        {
            TableName = "events",
            FilterExpression = "developer_id = :devId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":devId", new AttributeValue { S = developer_id } }
            }
        };

        var response = await dynamoDb.ScanAsync(request);
        var eventsList = new List<object>();

        foreach (var item in response.Items)
        {
            eventsList.Add(new
            {
                event_id = item["event_id"].S,
                developer_id = item["developer_id"].S,
                metric_type = item["metric_type"].S,
                value = double.Parse(item["value"].N),
                repository = item["repository"].S,
                timestamp = item["timestamp"].S
            });
        }

        return Results.Ok(eventsList);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao buscar eventos: {ex.Message}", statusCode: 500);
    }
});

// Endpoint 2: Obter o Sumário Agregado/Consolidado do Desenvolvedor
app.MapGet("/metrics/{developer_id}/summary", async (string developer_id, IAmazonDynamoDB dynamoDb) =>
{
    try
    {
        var request = new GetItemRequest
        {
            TableName = "developer_summary",
            Key = new Dictionary<string, AttributeValue>
            {
                { "developer_id", new AttributeValue { S = developer_id } }
            }
        };

        var response = await dynamoDb.GetItemAsync(request);

        if (!response.IsItemSet)
        {
            return Results.NotFound(new { message = $"Sumário para o desenvolvedor {developer_id} não encontrado." });
        }

        var item = response.Item;
        var summary = new
        {
            developer_id = item["developer_id"].S,
            total_commits = int.Parse(item["total_commits"].N),
            total_pull_requests = int.Parse(item["total_pull_requests"].N),
            avg_review_time_minutes = double.Parse(item["avg_review_time_minutes"].N.Replace(".", ",")),
            events_processed = int.Parse(item["events_processed"].N),
            last_activity = item["last_activity"].S
        };

        return Results.Ok(summary);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao buscar sumário: {ex.Message}", statusCode: 500);
    }
});

// Força a API a rodar na porta interna configurada pelo Docker Compose
app.Run();