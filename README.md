# Developer Metrics Pipeline

Pipeline de dados orientado a eventos (EDA) desenvolvido em **.NET 8** para coleta, processamento e agregação de métricas de produtividade de desenvolvedores em tempo real.

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
* **Linguagem:** C# (.NET 8)
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
git clone [https://github.com/JTBCode86/developer-metrics-pipeline.git](https://github.com/JTBCode86/developer-metrics-pipeline.git)
cd developer-metrics-pipeline
```

**O que este comando faz:**

* **Infraestrutura:** Subida do ambiente LocalStack.
* **Provisionamento:** Criação automática das filas SQS e tabelas DynamoDB.
* **Carga Inicial:** Disparo do *seed* de eventos conforme requisitos do projeto.
* **Execução:** Inicialização de todos os serviços (Workers) prontos para processar os dados.
 
## 📊 Observabilidade
Assim que o comando for executado, o terminal unificará os logs de todos os componentes. Você poderá acompanhar em tempo real:

* **Processor:** Validação e enriquecimento de contratos (UUID v4).
* **Aggregator:** Consolidação e exibição do painel analítico das métricas processadas.

## 🛡️ Engenharia e Boas Práticas
* **Resiliência:** Processamento *At-Least-Once*.
* **Concorrência:** Gerenciamento seguro de estado via coleções concorrentes.
* **Escalabilidade:** Estrutura pronta para produção com *Single-Table Design* no DynamoDB.