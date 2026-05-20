#!/bin/bash

echo "========================================================================"
echo "🚀 Iniciando Carga de Testes (Seed) para Fila SQS: raw-events"
echo "========================================================================"

enviar_evento() {
    local payload=$1
    awslocal sqs send-message --queue-url http://localstack:4566/000000000000/raw-events --message-body "$payload" > /dev/null
}

# --- 1. MENSAGENS VÁLIDAS ---
# Desenvolvedor 1 (jean-dev) 
enviar_evento '{"event_id": "a1111111-1111-4111-1111-111111111111", "developer_id": "jean-dev", "metric_type": "commits", "value": 5, "repository": "org/api-payment", "timestamp": "2026-05-19T10:00:00Z"}'
enviar_evento '{"event_id": "a2222222-2222-4222-2222-222222222222", "developer_id": "jean-dev", "metric_type": "commits", "value": 12, "repository": "org/api-payment", "timestamp": "2026-05-19T10:15:00Z"}'
enviar_evento '{"event_id": "a3333333-3333-4333-3333-333333333333", "developer_id": "jean-dev", "metric_type": "pull_requests", "value": 1, "repository": "org/api-payment", "timestamp": "2026-05-19T10:30:00Z"}'
enviar_evento '{"event_id": "a4444444-4444-4444-4444-444444444444", "developer_id": "jean-dev", "metric_type": "review_time_minutes", "value": 45, "repository": "org/api-payment", "timestamp": "2026-05-19T11:00:00Z"}'
enviar_evento '{"event_id": "a5555555-5555-4555-5555-555555555555", "developer_id": "jean-dev", "metric_type": "review_time_minutes", "value": 15, "repository": "org/web-portal", "timestamp": "2026-05-19T11:30:00Z"}'

# Desenvolvedor 2 (ana-data) 
enviar_evento '{"event_id": "b1111111-1111-4111-b111-111111111111", "developer_id": "ana-data", "metric_type": "commits", "value": 20, "repository": "org/data-pipeline", "timestamp": "2026-05-19T09:00:00Z"}'
enviar_evento '{"event_id": "b2222222-2222-4222-b222-222222222222", "developer_id": "ana-data", "metric_type": "commits", "value": 8, "repository": "org/data-pipeline", "timestamp": "2026-05-19T09:45:00Z"}'
enviar_evento '{"event_id": "b3333333-3333-4333-b333-333333333333", "developer_id": "ana-data", "metric_type": "pull_requests", "value": 3, "repository": "org/data-pipeline", "timestamp": "2026-05-19T13:00:00Z"}'
enviar_evento '{"event_id": "b4444444-4444-4444-b444-444444444444", "developer_id": "ana-data", "metric_type": "review_time_minutes", "value": 120, "repository": "org/data-pipeline", "timestamp": "2026-05-19T14:20:00Z"}'

# Desenvolvedor 3 (lucas-tech) 
enviar_evento '{"event_id": "c1111111-1111-4111-c111-111111111111", "developer_id": "lucas-tech", "metric_type": "commits", "value": 1, "repository": "org/infra-core", "timestamp": "2026-05-19T07:10:00Z"}'
enviar_evento '{"event_id": "c2222222-2222-4222-c222-222222222222", "developer_id": "lucas-tech", "metric_type": "pull_requests", "value": 1, "repository": "org/infra-core", "timestamp": "2026-05-19T08:00:00Z"}'
enviar_evento '{"event_id": "c3333333-3333-4333-c333-333333333333", "developer_id": "lucas-tech", "metric_type": "review_time_minutes", "value": 30, "repository": "org/infra-core", "timestamp": "2026-05-19T08:40:00Z"}'
enviar_evento '{"event_id": "c4444444-4444-4444-c444-444444444444", "developer_id": "lucas-tech", "metric_type": "commits", "value": 4, "repository": "org/infra-core", "timestamp": "2026-05-19T15:00:00Z"}'
enviar_evento '{"event_id": "c5555555-5555-4555-c555-555555555555", "developer_id": "lucas-tech", "metric_type": "commits", "value": 2, "repository": "org/infra-core", "timestamp": "2026-05-19T16:10:00Z"}'
enviar_evento '{"event_id": "c6666666-6666-4666-c666-666666666666", "developer_id": "lucas-tech", "metric_type": "pull_requests", "value": 2, "repository": "org/infra-core", "timestamp": "2026-05-19T17:00:00Z"}'
enviar_evento '{"event_id": "c7777777-7777-4777-c777-777777777777", "developer_id": "lucas-tech", "metric_type": "review_time_minutes", "value": 60, "repository": "org/infra-core", "timestamp": "2026-05-19T17:30:00Z"}'

# --- 2. MENSAGENS DUPLICADAS (IDs CORRIGIDOS para UUID v4) ---
enviar_evento '{"event_id": "a1111111-1111-4111-1111-111111111111", "developer_id": "jean-dev", "metric_type": "commits", "value": 5, "repository": "org/api-payment", "timestamp": "2026-05-19T10:00:00Z"}'
enviar_evento '{"event_id": "b3333333-3333-4333-b333-333333333333", "developer_id": "ana-data", "metric_type": "pull_requests", "value": 3, "repository": "org/data-pipeline", "timestamp": "2026-05-19T13:00:00Z"}'

# --- 3. MENSAGENS INVÁLIDAS (Mantidas como estavam) ---
enviar_evento '{"event_id": "d1111111-1111-4111-d111-111111111111", "developer_id": "jean-dev", "metric_type": "review_time_minutes", "value": 1500, "repository": "org/api-payment", "timestamp": "2026-05-19T12:00:00Z"}'
enviar_evento '{"event_id": "ID-INVALIDO-TESTE-DLQ", "developer_id": "lucas-tech", "metric_type": "commits", "value": 1, "repository": "org/infra-core", "timestamp": "2026-05-19T12:30:00Z"}'

echo "========================================================================"
echo "🎯 Carga concluída com sucesso!"
echo "========================================================================"