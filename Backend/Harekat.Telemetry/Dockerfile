FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Harekat.Telemetry.Domain/Harekat.Telemetry.Domain.csproj src/Harekat.Telemetry.Domain/
COPY src/Harekat.Telemetry.Application/Harekat.Telemetry.Application.csproj src/Harekat.Telemetry.Application/
COPY src/Harekat.Telemetry.Infrastructure/Harekat.Telemetry.Infrastructure.csproj src/Harekat.Telemetry.Infrastructure/
COPY src/Harekat.Telemetry.Api/Harekat.Telemetry.Api.csproj src/Harekat.Telemetry.Api/
RUN dotnet restore src/Harekat.Telemetry.Api/Harekat.Telemetry.Api.csproj
COPY . .
RUN dotnet publish src/Harekat.Telemetry.Api/Harekat.Telemetry.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8090
EXPOSE 8090
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Harekat.Telemetry.Api.dll"]
