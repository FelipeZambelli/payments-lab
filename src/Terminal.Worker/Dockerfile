# Build context: raiz do repositório (ver deploy/compose/docker-compose.yml).
# Mesmo esquema da API: SDK na arquitetura do host, imagem final na arquitetura alvo (ARM64 no Mac M1).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Lab.Contracts/Lab.Contracts.csproj src/Lab.Contracts/
COPY src/Terminal.Worker/Terminal.Worker.csproj src/Terminal.Worker/
RUN dotnet restore src/Terminal.Worker/Terminal.Worker.csproj -a $TARGETARCH

COPY src/Lab.Contracts/ src/Lab.Contracts/
COPY src/Terminal.Worker/ src/Terminal.Worker/
RUN dotnet publish src/Terminal.Worker/Terminal.Worker.csproj -c Release -a $TARGETARCH --no-restore -o /app

# Worker não serve HTTP: basta o runtime, sem ASP.NET Core.
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
# Forma exec: o dotnet é o PID 1 e recebe o SIGTERM do "docker stop" diretamente.
ENTRYPOINT ["dotnet", "Terminal.Worker.dll"]
