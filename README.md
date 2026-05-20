# Developer Metrics Pipeline

Pipeline de dados orientado a eventos (EDA) desenvolvido em **.NET 8** para coleta, processamento e agregação de métricas de produtividade de desenvolvedores em tempo real.

## 📋 Pré-requisitos
Para executar este projeto, você precisará dos seguintes softwares instalados em sua máquina:

* **[Docker Desktop](https://www.docker.com/products/docker-desktop/):** Necessário para rodar os containers e o orquestrador Docker Compose.
* **.NET 8 SDK:** Caso deseje compilar ou realizar alterações no código dos serviços.
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
git clone [https://github.com/JTBCode86/developer-metrics-pipeline.git]
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

## 🛑 Como Parar o Projeto
```bash
docker-compose down
```
Caso deseje remover também os volumes (limpando completamente os dados persistidos no DynamoDB para um próximo teste "do zero"):
```bash
docker-compose down -v
```

## 🔧 Troubleshooting
Caso encontre problemas persistentes durante a orquestração ou alterações nos scripts não sejam aplicadas pelo Docker, você pode limpar o cache de build para forçar uma nova reconstrução das imagens:
```bash
docker builder prune -f
docker-compose up --build
```

## 📊 Observabilidade
Assim que o comando for executado, o terminal unificará os logs de todos os componentes. Você poderá acompanhar em tempo real:

* **Processor:** Validação e enriquecimento de contratos (UUID v4).
* **Aggregator:** Consolidação e exibição do painel analítico das métricas processadas.

## 🛡️ Engenharia e Boas Práticas
* **Resiliência:** Processamento *At-Least-Once*.
* **Concorrência:** Gerenciamento seguro de estado via coleções concorrentes.
* **Escalabilidade:** Estrutura pronta para produção com *Single-Table Design* no DynamoDB.

## 🤝 Contribuições
Contribuições são bem-vindas! Sinta-se à vontade para abrir uma *Issue* ou enviar um *Pull Request* caso encontre melhorias, correções de bugs ou novas funcionalidades para o pipeline.

---
*Desenvolvido com foco em arquitetura de sistemas distribuídos e boas práticas de desenvolvimento .NET*
