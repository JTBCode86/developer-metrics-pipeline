#!/bin/bash
echo "########### Inicializando Infraestrutura Local AWS (LocalStack) ###########"

# 1. Criar Filas SQS e suas respectivas DLQs
echo "-> Criando fila: raw-events-dlq"
awslocal sqs create-queue --queue-name raw-events-dlq

echo "-> Criando fila: raw-events (com Redrive Policy para DLQ)"
awslocal sqs create-queue --queue-name raw-events \
  --attributes '{"RedrivePolicy": "{\"deadLetterTargetArn\":\"arn:aws:sqs:us-east-1:000000000000:raw-events-dlq\",\"maxReceiveCount\":\"3\"}"}'

echo "-> Criando fila: processed-events-dlq"
awslocal sqs create-queue --queue-name processed-events-dlq

echo "-> Criando fila: processed-events (com Redrive Policy para DLQ)"
awslocal sqs create-queue --queue-name processed-events \
  --attributes '{"RedrivePolicy": "{\"deadLetterTargetArn\":\"arn:aws:sqs:us-east-1:000000000000:processed-events-dlq\",\"maxReceiveCount\":\"3\"}"}'

# 2. Criar Tabelas no DynamoDB
echo "-> Criando tabela DynamoDB: events"
awslocal dynamodb create-table \
  --table-name events \
  --attribute-definitions AttributeName=event_id,AttributeType=S \
  --key-schema AttributeName=event_id,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST

echo "-> Criando tabela DynamoDB: developer_summary"
awslocal dynamodb create-table \
  --table-name developer_summary \
  --attribute-definitions AttributeName=developer_id,AttributeType=S \
  --key-schema AttributeName=developer_id,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST

echo "########### Infraestrutura AWS provisionada com sucesso! ###########"
echo "########### Carregar seed! ###########"
bash /scripts/seed.sh
echo "########### seed carregado com sucesso! ###########"