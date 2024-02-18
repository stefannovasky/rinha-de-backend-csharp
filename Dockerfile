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
RUN dotnet build "./Rinha.Web/Rinha.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -r linux-x64 "./Rinha.Web/Rinha.Web.csproj" -c Release -o /app/publish /p:UseAppHost=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Rinha.Web"]
