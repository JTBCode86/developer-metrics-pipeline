# Developer Metrics Pipeline

![.NET 9](https://img.shields.io/badge/.NET-9-blue.svg)
![Docker](https://img.shields.io/badge/Docker-Enabled-brightgreen.svg)
![Architecture](https://img.shields.io/badge/Architecture-EDA-orange.svg)

Pipeline de dados orientado a eventos (EDA) desenvolvido em **.NET 8** para coleta, processamento e agregação de métricas de produtividade de desenvolvedores em tempo real.

## 📋 Pré-requisitos
Para executar este projeto, você precisará dos seguintes softwares instalados em sua máquina:

* **[Docker Desktop](https://www.docker.com/products/docker-desktop/):** Necessário para rodar os containers e o orquestrador Docker Compose.
* **.NET 9 SDK:** Caso deseje compilar ou realizar alterações no código dos serviços.
* **Git:** Para clonar o repositório.

*Certifique-se de que o Docker esteja em execução antes de iniciar o projeto.*

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

## 🛠️ Stack Tecnológica
* **Linguagem:** C# (.NET 9)
* **Padrão:** BackgroundServices (Worker SDK)
* **Orquestração:** Docker & Docker Compose
* **Mensageria:** Amazon SQS
* **Persistência:** Amazon DynamoDB
* **Ambiente Local:** **LocalStack** (Emulação completa e isolada dos serviços AWS, garantindo consistência total do ambiente de desenvolvimento).

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
O sistema expõe sua interface de integração através do **Swagger**, permitindo a exploração visual e o teste dos contratos de dados em tempo real.

* **Interface:** Após subir o ambiente via Docker, acesse `http://localhost:5000/swagger`.
* **Projeto:** **Metrics Processor API** (v1)
* **Endpoints Disponíveis:**
    * `GET /health`: Monitoramento de prontidão (*Health Check*).
    * `GET /metrics/{developer_id}`: Recuperação de eventos brutos processados.
    * `GET /metrics/{developer_id}/summary`: Consulta consolidada com métricas agregadas e cálculo de média sob demanda.

> **Dica:** A documentação interativa é gerada dinamicamente, garantindo que o contrato de integração esteja sempre atualizado com a implementação do código.

## 📚 Referência Técnica: Swagger (OpenAPI)

Para facilitar a integração e o teste dos endpoints, este projeto utiliza o **Swagger (OpenAPI)**. Ele gera automaticamente uma documentação interativa baseada nos *endpoints* definidos na aplicação.

**Principais funcionalidades documentadas:**

* **Exploração:** Visualização de todos os verbos HTTP disponíveis (`GET`).
* **Teste em tempo real:** Permite disparar requisições diretamente pela interface, preenchendo os parâmetros de rota (`developer_id`) e recebendo a resposta formatada em JSON.
* **Contrato de Dados:** Define os modelos esperados, garantindo que qualquer desenvolvedor que consuma a API saiba exatamente a estrutura (*schema*) do objeto que será retornado.

**Como utilizar em desenvolvimento:**
1. Com o projeto em execução (`docker-compose up`), acesse o endereço da porta exposta (ex: `http://localhost:5000/swagger`).
2. Clique no endpoint desejado (ex: `/metrics/{developer_id}/summary`).
3. Clique em **"Try it out"**.
4. Informe o ID do desenvolvedor e clique em **"Execute"** para ver o resultado em tempo real.
   
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

## 🤝 Contribuições
Contribuições são bem-vindas! Sinta-se à vontade para abrir uma *Issue* ou enviar um *Pull Request* caso encontre melhorias, correções de bugs ou novas funcionalidades para o pipeline.

---
*Desenvolvido com foco em arquitetura de sistemas distribuídos e boas práticas de desenvolvimento .NET*
