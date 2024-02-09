#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
RUN apt-get update \
  && apt-get install -y --no-install-recommends \
  clang zlib1g-dev
WORKDIR /src
COPY ["./src", "."]
RUN dotnet restore "./Rinha.Web/Rinha.Web.csproj"
# COPY . .
# WORKDIR "."
RUN dotnet build "./Rinha.Web/Rinha.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./Rinha.Web/Rinha.Web.csproj" -c Release -o /app/publish /p:UseAppHost=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV POSTGRESQL_CONNECTION_STRING="Host=postgres-db;Username=root;Password=root;Database=rinha-db;MaxPoolSize=30;MinPoolSize=5;Connection Pruning Interval=1;Connection Idle Lifetime=2;Enlist=false;No Reset On Close=true;Pooling=true"
ENTRYPOINT ["./Rinha.Web"]
