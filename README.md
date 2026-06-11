# Developer Metrics Pipeline

![.NET 9](https://img.shields.io/badge/.NET-9-blue.svg)
![Docker](https://img.shields.io/badge/Docker-Enabled-brightgreen.svg)
![Architecture](https://img.shields.io/badge/Architecture-EDA-orange.svg)

Pipeline de dados orientado a eventos (EDA) desenvolvido em **.NET 9** para coleta, processamento e agregação de métricas de produtividade de desenvolvedores em tempo real.

## 📋 Pré-requisitos
Para executar este projeto, você precisará dos seguintes softwares instalados em sua máquina:

* **[Docker Desktop](https://www.docker.com/products/docker-desktop/):** Necessário para rodar os containers e o orquestrador Docker Compose.
* **.NET 9 SDK:** Caso deseje compilar ou realizar alterações no código dos serviços.
* **Git:** Para clonar o repositório.

*Certifique-se de que o Docker esteja em execução antes de iniciar o projeto.*

## 🛠️ Stack Tecnológica
* **Linguagem:** C# (.NET 9)
* **Padrão:** BackgroundServices (Worker SDK)
* **Orquestração:** Docker & Docker Compose
* **Mensageria:** Amazon SQS
* **Persistência:** Amazon DynamoDB
* **Ambiente Local:** **LocalStack** (Emulação completa e isolada dos serviços AWS, garantindo consistência total do ambiente de desenvolvimento).
  
## 🏗️ Arquitetura do Sistema
O sistema foi projetado seguindo princípios de **Event-Driven Architecture (EDA)**, focando em escalabilidade e resiliência:

* **Desacoplamento:** A comunicação é feita exclusivamente via mensageria (Amazon SQS), garantindo que os serviços de processamento e agregação operem de forma assíncrona e independente.
* **Infraestrutura como Código (IaC):** Através do **LocalStack**, todo o ecossistema AWS (SQS e DynamoDB) é provisionado automaticamente. Isso elimina qualquer configuração manual, garantindo um ambiente de desenvolvimento idêntico ao de produção.
* **Resiliência e Consistência:** O fluxo de dados foi desenhado para suportar falhas transientes, utilizando o padrão de entrega *At-Least-Once*, onde o ciclo de vida da mensagem é controlado para assegurar que nenhum evento de métrica seja perdido.

## 🛡️ Padrões de Arquitetura
O projeto foi estruturado seguindo princípios de **Clean Architecture** e **SOLID**, garantindo um código de alta manutenibilidade:

* **Desacoplamento de Infraestrutura:** A lógica de negócio é agnóstica em relação aos serviços de mensageria. Utilizamos interfaces para isolar o SDK da AWS, permitindo a fácil substituição de provedores de mensageria ou banco de dados.
* **Separation of Concerns (SoC):** Cada *Worker* possui responsabilidade única (*Single Responsibility Principle*). O `Processor` preocupa-se estritamente com validação e enriquecimento, enquanto o `Aggregator` foca exclusivamente na consolidação e persistência dos dados.
* **Testabilidade:** A separação entre o domínio (contratos de métricas) e a infraestrutura facilita a criação de testes unitários para a regra de negócio.

## 🚀 Melhorias de Arquitetura e Engenharia

Para garantir resiliência e integridade dos dados sob alta carga, o projeto passou por uma refatoração focada em padrões de sistemas distribuídos:

## ✅ Validação e Testes
O projeto conta com uma estratégia de validação de ponta a ponta, garantindo a integridade e a confiabilidade dos dados processados pelo pipeline:

* **Teste de Carga/Integração:** Validação realizada através de disparo de carga simultânea via interface Swagger e terminal (PowerShell/CMD), garantindo a comunicação fluida entre `Processor` e `Aggregator`.
* **Consistência:** Verificada via CLI do LocalStack (executando comandos `dynamodb scan` diretamente no container), confirmando que os contadores (`total_commits`, `total_pull_requests`) e os cálculos de média (`avg_review_time_minutes`) refletem com precisão absoluta os eventos processados.
* **Resiliência:** O sistema foi submetido a cenários de falha, incluindo o envio de mensagens inválidas, validando o roteamento correto para a **DLQ** e a robustez dos logs de erro em tempo real.

### 1. Atomicidade e Consistência (Database-as-the-Brain)
* **Problema:** O padrão original *Get-Modify-Put* sofria de *Race Conditions*, onde atualizações simultâneas causavam perda de dados.
* **Solução:** Implementamos operações atômicas via `UpdateExpression` (`ADD`) no DynamoDB. Agora, a lógica de soma é processada nativamente pelo banco de dados, garantindo consistência total mesmo com múltiplas instâncias do Worker acessando o mesmo registro simultaneamente.

### 2. Idempotência Nativa
* **Problema:** Sistemas de mensageria (como SQS) garantem entrega "pelo menos uma vez", o que inevitavelmente gera duplicatas.
* **Solução:** Utilizamos `ConditionExpressions` nas operações de atualização (`attribute_not_exists`). Isso garante que cada `event_id` seja processado exatamente uma vez, bloqueando qualquer reprocessamento indevido sem a necessidade de chamadas de leitura (*GetItem*) extras, otimizando o consumo de custos (RCU/WCU).

### 3. Resiliência e Observabilidade (DLQ)
* **Problema:** Mensagens inválidas bloqueavam o fluxo de processamento ou eram descartadas silenciosamente.
* **Solução:** Adicionamos uma **Dead Letter Queue (DLQ)**. Eventos que falham na validação de esquema são automaticamente desviados para uma fila de análise (`raw-events-dlq`), permitindo auditoria e depuração sem interromper o pipeline principal.

### 4. Monitoramento e Diagnóstico
* Logs estruturados utilizando `BeginScope` para rastreabilidade de `EventId`.
* Tratamento de *Graceful Shutdown* para garantir que todas as mensagens em processamento sejam finalizadas ou tratadas corretamente antes do encerramento do container.

### 5. Resiliência do Consumer (Worker)
* **Problema:** Erros intermitentes no loop de processamento (ex: respostas vazias do SQS ou nulos inesperados) causavam reinicialização constante dos serviços.
* **Solução:** Implementamos lógica defensiva de Null-safety e tratamento robusto de exceções no loop ExecuteAsync. O Worker agora é capaz de tolerar falhas transientes e aguardar novas mensagens de forma resiliente, sem impacto na estabilidade da aplicação.

---

### Fluxo do Pipeline
```mermaid
graph LR
    A[Raw Queue] --> B{Processor}
    B -->|Válido| D[Processed Queue]
    D --> E{Aggregator}
    E --> F[(DynamoDB)]
    F --> G[API de Consulta]
    G -->|Calcula Média na Leitura| H[Resposta Final]

```
  
---

## 🚀 Como Executar o Projeto

Para rodar o ecossistema completo, basta realizar o clone do repositório e executar o comando de orquestração. O ambiente configurado cuidará automaticamente da criação da infraestrutura, provisionamento dos recursos AWS e injeção da carga de mensagens de teste.

### 1. Clonar o Repositório
```bash
git clone https://github.com/JTBCode86/developer-metrics-pipeline.git
```
### 2. Abra o seu terminal, navegue até a pasta developer-metrics-pipeline e execute o comando abaixo
```bash
cd developer-metrics-pipeline
```
### 3. Executar o comando abaixo
```bash
docker-compose up --build
```
**O que este comando faz:**

* **Infraestrutura:** Subida do ambiente LocalStack.
* **Provisionamento:** Criação automática das filas SQS e tabelas DynamoDB.
* **Carga Inicial:** Disparo do *seed* de eventos conforme requisitos do projeto.
* **Execução:** Inicialização de todos os serviços (Workers) prontos para processar os dados.

## 📝 Documentação da API (Swagger/OpenAPI)

Este projeto utiliza o **Swagger (OpenAPI)** para documentação de contrato. Esta escolha estratégica garante que a documentação reflita exatamente o estado atual do código, eliminando discrepâncias entre o que está implementado e o que está disponível.

### Por que esta abordagem?
* **Sincronização:** O contrato de dados (schemas) é exposto automaticamente, permitindo que consumidores da API entendam os tipos de dados e obrigatoriedades antes mesmo de realizarem a integração.
* **Autonomia:** Desenvolvedores podem explorar e validar comportamentos da API diretamente pela interface interativa, reduzindo drasticamente o tempo de *debug* e testes de integração.

### Como utilizar em desenvolvimento
Após subir o ambiente via Docker (`docker-compose up`), a documentação interativa estará disponível para exploração e testes:

1. **Acesso:** Acesse `http://localhost:5000/swagger` no seu navegador.
2. **Exploração:** Visualize todos os verbos HTTP disponíveis e os modelos de dados (schemas).
3. **Teste em tempo real:**
    * Clique no endpoint desejado (ex: `/metrics/{developer_id}/summary`).
    * Clique no botão **"Try it out"**.
    * Informe o parâmetro necessário (ex: `developer_id`) e clique em **"Execute"** para ver a resposta formatada em JSON.

**Endpoints Principais:**

* `POST /api/eventos`: Envio de novos eventos de métricas para o pipeline.
    * **Payload Exemplo:**
        ```json
        {
          "event_id": "evt-777",
          "developer_id": "juca-dev",
          "metric_type": "commits",
          "value": 10,
          "repository": "meu-projeto",
          "timestamp": "2026-06-10T23:05:00Z"
        }
        ```

* `GET /health`: Monitoramento de prontidão (*Health Check*).
* `GET /metrics/{developer_id}`: Recuperação de eventos brutos processados.
* `GET /metrics/{developer_id}/summary`: Consulta consolidada com métricas agregadas e cálculo de média sob demanda.

 *Dica:* A documentação interativa é gerada dinamicamente, garantindo que o contrato de integração esteja sempre atualizado com a implementação do código.*

## 📊 Observabilidade e Monitoramento
A observabilidade é unificada no terminal do Docker, permitindo o acompanhamento em tempo real:

* **Processor:** Log detalhado de validação de contratos e enriquecimento de eventos (UUID v4).
* **Aggregator:** Processamento assíncrono e exibição de métricas persistidas.
* **API de Consulta:** Interface REST (via Swagger) para consulta em tempo real das métricas agregadas por desenvolvedor, com cálculos de performance realizados no momento da leitura.

## 🛡️ Engenharia e Boas Práticas
* **Resiliência:** Processamento *At-Least-Once*.
* **Concorrência:** Gerenciamento seguro de estado via coleções concorrentes.
* **Escalabilidade:** Estrutura pronta para produção com *Single-Table Design* no DynamoDB.

## 🔧 Troubleshooting
Caso encontre problemas persistentes durante a orquestração ou alterações nos scripts não sejam aplicadas pelo Docker, você pode limpar o cache de build para forçar uma nova reconstrução das imagens:

```bash
docker system prune -a --volumes
docker builder prune -f
docker-compose up --build
```

Consultar Logs
```bash
docker logs -f metrics-processor
docker logs -f metrics-aggregator
```
Consultar direto no DynamoDb
```bash
aws dynamodb list-tables --endpoint-url http://localhost:4566 --region us-east-1
aws dynamodb scan --table-name events --endpoint-url http://localhost:4566 --region us-east-1
aws dynamodb scan --table-name developer_summary --endpoint-url http://localhost:4566 --region us-east-1
```


> **⚠️ Nota de Configuração:** Certifique-se de que os arquivos de script e configuração (`seed.sh`, `init-aws.sh`, `docker-compose.yml` e `Dockerfile`) estejam salvos com codificação **UTF-8** e final de linha **LF (Unix)**. Arquivos salvos com codificação Windows (CRLF) podem causar erros de sintaxe ou falhas de execução dentro dos contêineres Linux.

*Dica de CLI: Ao executar comandos aws dynamodb via terminal Windows (CMD/PowerShell) contra o container, atente-se às aspas. O uso de docker exec -it <container_nome> awslocal ... requer tratamento específico de aspas no JSON. Em caso de erro de sintaxe, prefira listar os itens com scan ou utilize o PowerShell para garantir a correta interpretação do JSON.*

---

## 🤝 Contribuições
Contribuições são bem-vindas! Sinta-se à vontade para abrir uma *Issue* ou enviar um *Pull Request* caso encontre melhorias, correções de bugs ou novas funcionalidades para o pipeline.

---
*Desenvolvido com foco em arquitetura de sistemas distribuídos e boas práticas de desenvolvimento .NET*
