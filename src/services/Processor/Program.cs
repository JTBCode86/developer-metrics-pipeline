using Amazon.DynamoDBv2;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Processor;

var host = Host.CreateDefaultBuilder(args)
    // Configura o sistema de logs estruturados em JSON para o Worker
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ ";
        });
    })
    .ConfigureServices((hostContext, services) =>
    {
        // Força o host a carregar as variáveis de ambiente injetadas pelo Docker
        var configuration = hostContext.Configuration;

        // 1. Configuração e Injeção do cliente SQS (Mapeamento flexível para LocalStack)
        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var serviceUrl = configuration["AWS:ServiceURL"] ?? configuration["AWS__ServiceURL"] ?? "http://localhost:4566";
            var region = configuration["AWS:Region"] ?? configuration["AWS__Region"] ?? "us-east-1";

            var config = new AmazonSQSConfig
            {
                ServiceURL = serviceUrl,
                AuthenticationRegion = region
            };
            return new AmazonSQSClient(config);
        });

        // 2. Configuração e Injeção do cliente DynamoDB (Evita NullReference no processamento)
        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var serviceUrl = configuration["AWS:ServiceURL"] ?? configuration["AWS__ServiceURL"] ?? "http://localhost:4566";
            var region = configuration["AWS:Region"] ?? configuration["AWS__Region"] ?? "us-east-1";

            var config = new AmazonDynamoDBConfig
            {
                ServiceURL = serviceUrl,
                AuthenticationRegion = region
            };
            return new AmazonDynamoDBClient(config);
        });

        // 3. Registra o Worker que executa o loop de polling em background
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();