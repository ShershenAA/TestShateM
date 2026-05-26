# AutoParts B2B Platform 🔧

Pet-проект: B2B платформа продажи автозапчастей на микросервисной архитектуре.

## Стек технологий

| Слой | Технологии |
|------|-----------|
| Backend | .NET 10, ASP.NET Core, EF Core |
| Frontend | Angular 18, SignalR |
| Базы данных | PostgreSQL, MSSQL, Redis, Elasticsearch |
| Messaging | RabbitMQ |
| Observability | Prometheus, Grafana, ELK (Elasticsearch + Logstash + Kibana) |
| Infrastructure | Docker, Docker Compose, Kubernetes (Minikube) |

## Структура репозитория

```
autoparts/
├── docker-compose.yml          # Вся инфраструктура одной командой
├── docker/
│   ├── postgres/
│   │   └── init.sql            # Создание БД при старте
│   └── logstash/
│       ├── logstash.yml
│       └── pipeline/
│           └── logstash.conf   # Правила парсинга логов
│
├── src/
│   ├── Gateway/                # API Gateway на YARP
│   │   ├── Program.cs
│   │   ├── appsettings.json    # Маршруты к сервисам
│   │   └── Dockerfile
│   │
│   └── services/
│       ├── Catalog.API/        # Каталог запчастей
│       │   ├── Controllers/    # REST endpoints
│       │   ├── Models/         # Entities + DTOs
│       │   ├── Data/           # DbContext + Migrations
│       │   ├── Services/       # Бизнес-логика
│       │   ├── Consumers/      # RabbitMQ consumers
│       │   └── Dockerfile
│       │
│       ├── Orders.API/         # Заказы
│       │   ├── Controllers/
│       │   ├── Models/
│       │   ├── Data/
│       │   ├── Services/
│       │   ├── Consumers/
│       │   └── Dockerfile
│       │
│       ├── Inventory.API/      # Склад / остатки
│       │   ├── Controllers/
│       │   ├── Models/
│       │   ├── Data/
│       │   ├── Services/
│       │   ├── Consumers/      # Слушает OrderCreated, обновляет склад
│       │   └── Dockerfile
│       │
│       └── Notifications.API/  # SignalR уведомления
│           ├── Hubs/           # OrderStatusHub.cs
│           ├── Services/
│           ├── Consumers/      # Слушает события, пушит в SignalR
│           └── Dockerfile
│
├── angular-client/             # SPA клиент
│   └── src/app/
│       ├── components/         # catalog, orders, cart, notifications
│       └── services/           # http + signalr сервисы
│
└── infra/
    ├── k8s/                    # Kubernetes манифесты (Minikube)
    │   ├── deployments/
    │   ├── services/
    │   └── configmaps/
    ├── prometheus/
    │   └── prometheus.yml      # Scrape config для всех сервисов
    └── grafana/
        ├── provisioning/       # Auto-provisioning datasources
        └── dashboards/         # JSON дашборды
```

## Быстрый старт

### 1. Поднять инфраструктуру

```bash
# Клонировать / перейти в папку
cd autoparts

# Поднять только инфраструктурные сервисы (без .NET)
docker compose up -d postgres mssql redis rabbitmq elasticsearch kibana logstash prometheus grafana

# Проверить что всё живо
docker compose ps
```

### 2. Запустить сервисы локально (для разработки)

```bash
# Catalog API
cd src/services/Catalog.API
dotnet run

# Orders API
cd src/services/Orders.API
dotnet run
```

### 3. Или поднять всё целиком

```bash
docker compose up -d --build
```

## UI адреса

| Сервис | URL | Логин |
|--------|-----|-------|
| API Gateway | http://localhost:5000 | — |
| Catalog API (swagger) | http://localhost:5001/swagger | — |
| Orders API (swagger) | http://localhost:5002/swagger | — |
| Inventory API (swagger) | http://localhost:5003/swagger | — |
| Notifications API | http://localhost:5004/swagger | — |
| RabbitMQ Management | http://localhost:15672 | autoparts / autoparts_pass |
| Kibana | http://localhost:5601 | — |
| Prometheus | http://localhost:9090 | — |
| Grafana | http://localhost:3000 | admin / autoparts_admin |

## Порядок разработки

1. ✅ Инфраструктура — `docker-compose.yml` готов
2. ⬜ **Catalog.API** — CRUD + EF Core + PostgreSQL
3. ⬜ Elasticsearch — полнотекстовый поиск по запчастям
4. ⬜ **Orders.API** + RabbitMQ — создание заказов и события
5. ⬜ **Inventory.API** — подписка на `OrderCreated`, обновление остатков
6. ⬜ Redis кэш в Catalog + **Notifications.API** + SignalR
7. ⬜ **API Gateway** (YARP)
8. ⬜ Serilog → Logstash → Elasticsearch → Kibana
9. ⬜ prometheus-net в каждом сервисе → Grafana дашборды
10. ⬜ **Angular** клиент
11. ⬜ **Kubernetes** (Minikube) — манифесты в `infra/k8s/`

## События в RabbitMQ

```
OrderCreated     → Inventory.API (резервирует товар)
                 → Notifications.API (уведомляет дилера)

OrderConfirmed   → Notifications.API (статус обновлён)

StockUpdated     → Catalog.API (обновляет кэш в Redis)
```

## NuGet пакеты (ключевые)

```xml
<!-- Все сервисы -->
<PackageReference Include="Serilog.AspNetCore" />
<PackageReference Include="Serilog.Sinks.Http" />          <!-- → Logstash -->
<PackageReference Include="prometheus-net.AspNetCore" />
<PackageReference Include="MassTransit.RabbitMQ" />

<!-- Catalog.API -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
<PackageReference Include="NEST" />                          <!-- Elasticsearch -->
<PackageReference Include="StackExchange.Redis" />

<!-- Orders.API -->
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />

<!-- Gateway -->
<PackageReference Include="Yarp.ReverseProxy" />

<!-- Notifications.API -->
<PackageReference Include="Microsoft.AspNetCore.SignalR" />
<PackageReference Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" />
```
