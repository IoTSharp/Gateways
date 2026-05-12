FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish src/IoTEdge.DeviceSimulator/IoTEdge.DeviceSimulator.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
EXPOSE 1502

ENTRYPOINT ["dotnet", "IoTEdge.DeviceSimulator.dll"]
