# Этап сборки
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем csproj и восстанавливаем зависимости отдельным слоем
# (Docker кэширует этот слой если csproj не менялся — сборка быстрее)
COPY ["Orders.API.csproj", "."]
RUN dotnet restore

# Копируем остальной код и собираем
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Этап запуска — образ меньше чем sdk
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Orders.API.dll"]