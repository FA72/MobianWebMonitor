FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY src/MobianWebMonitor/MobianWebMonitor.csproj src/MobianWebMonitor/
RUN dotnet restore src/MobianWebMonitor/MobianWebMonitor.csproj -a $TARGETARCH

COPY src/MobianWebMonitor/ src/MobianWebMonitor/
WORKDIR /src/src/MobianWebMonitor
RUN dotnet publish MobianWebMonitor.csproj -a $TARGETARCH -c Release -o /app
RUN install -D /app/wwwroot/_framework/blazor.server.js /app/wwwroot/js/blazor.server.js

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends systemd \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /data/history && chown -R 1000:1000 /data

COPY --from=build /app .

USER 1000

EXPOSE 8082

ENV ASPNETCORE_URLS=http://+:8082 \
    DOTNET_RUNNING_IN_CONTAINER=true

HEALTHCHECK --interval=30s --timeout=5s --retries=3 --start-period=20s \
    CMD ["dotnet", "MobianWebMonitor.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "MobianWebMonitor.dll"]
