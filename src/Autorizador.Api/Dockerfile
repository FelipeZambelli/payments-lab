# Build context: raiz do repositório (ver deploy/compose/docker-compose.yml).
# O SDK roda na arquitetura do host (BUILDPLATFORM) e publica para a arquitetura alvo (TARGETARCH),
# então no Mac M1 a imagem final é ARM64 nativa.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Lab.Contracts/Lab.Contracts.csproj src/Lab.Contracts/
COPY src/Autorizador.Api/Autorizador.Api.csproj src/Autorizador.Api/
RUN dotnet restore src/Autorizador.Api/Autorizador.Api.csproj -a $TARGETARCH

COPY src/Lab.Contracts/ src/Lab.Contracts/
COPY src/Autorizador.Api/ src/Autorizador.Api/
RUN dotnet publish src/Autorizador.Api/Autorizador.Api.csproj -c Release -a $TARGETARCH --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Autorizador.Api.dll"]
