# AutoParts B2B Platform

B2B платформа для оптовых закупок автозапчастей на микросервисной архитектуре.

## Архитектура
```
Angular Client
↓
API Gateway (YARP)
↓
┌─────────────────────────────────────┐
│  Catalog.API  │  Orders.API         │
│  PostgreSQL   │  MSSQL              │
│  Redis Cache  │                     │
│  Elasticsearch│                     │
└─────────────────────────────────────┘
↓ RabbitMQ Events
┌─────────────────────────────────────┐
│  Inventory.API  │  Notifications.API│
│  PostgreSQL     │  SignalR + Redis  │
└─────────────────────────────────────┘
```
## Стек технологий

| Слой | Технологии |
|------|-----------|
| Backend | .NET 10, ASP.NET Core, EF Core |
| Frontend | Angular 18, SignalR |
| Базы данных | PostgreSQL, MSSQL, Redis, Elasticsearch |
| Messaging | RabbitMQ, MassTransit |
| Observability | Prometheus, Grafana, Serilog |
| Infrastructure | Docker, Docker Compose, Kubernetes |
| Gateway | YARP Reverse Proxy |

## Сервисы

| Сервис | Порт | Описание |
|--------|------|----------|
| API Gateway | 5000 | Единая точка входа, маршрутизация |
| Catalog.API | 5001 | Каталог запчастей, поиск |
| Orders.API | 5002 | Заказы |
| Inventory.API | 5003 | Склад, остатки |
| Notifications.API | 5004 | SignalR уведомления |
| RabbitMQ Management | 15672 | Мониторинг очередей |
| Grafana | 3000 | Метрики и дашборды |
| Kibana | 5601 | Логи |
| Prometheus | 9090 | Сбор метрик |

## Быстрый старт

### Требования
- Docker Desktop
- .NET 10 SDK
- Node.js 20+

### Запуск инфраструктуры

```bash
docker compose up -d
```

### Запуск Angular клиента

```bash
cd angular-client
npm install
ng serve
```

Открой http://localhost:4200

### Запуск в Kubernetes (Minikube)

```bash
minikube start
kubectl apply -f infra/k8s/namespace.yml
kubectl apply -f infra/k8s/configmaps/
kubectl apply -f infra/k8s/deployments/
minikube service gateway -n autoparts
```

## Флоу создания заказа

1. Дилер выбирает запчасти в каталоге Angular
2. Оформляет заказ → `Orders.API` сохраняет в MSSQL
3. `Orders.API` публикует событие `OrderCreated` в RabbitMQ
4. `Inventory.API` получает событие → проверяет остатки → резервирует товар
5. Публикует `OrderConfirmed` или `OrderRejected`
6. `Notifications.API` получает событие → отправляет push через SignalR
7. Angular клиент получает уведомление в реальном времени

## Credentials (для локальной разработки)

| Сервис | URL | Логин |
|--------|-----|-------|
| RabbitMQ | http://localhost:15672 | autoparts / autoparts_pass |
| Grafana | http://localhost:3000 | admin / autoparts_admin |