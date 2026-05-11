FROM node:22-alpine AS frontend
WORKDIR /src/ClientApp

COPY src/IoTSharp.Edge/ClientApp/package*.json ./
RUN npm ci --no-audit --no-fund

COPY src/IoTSharp.Edge/ClientApp/. ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
COPY --from=frontend /src/ClientApp/dist /src/src/IoTSharp.Edge/wwwroot
RUN dotnet publish src/IoTSharp.Edge/IoTSharp.Edge.csproj -c Release -o /app/publish /p:UseAppHost=false /p:BuildClientApp=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "IoTSharp.Edge.dll"]
