FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Harekat.Backend.sln ./
COPY Harekat.Domain/Harekat.Domain.csproj Harekat.Domain/
COPY Harekat.Application/Harekat.Application.csproj Harekat.Application/
COPY Harekat.Infrastructure/Harekat.Infrastructure.csproj Harekat.Infrastructure/
COPY Harekat.Api/Harekat.Api.csproj Harekat.Api/
COPY ClientSdk/Harekat.ClientSdk.csproj ClientSdk/
COPY Harekat.Tests/Harekat.Tests.csproj Harekat.Tests/
RUN dotnet restore Harekat.Api/Harekat.Api.csproj
COPY . .
RUN dotnet publish Harekat.Api/Harekat.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV Storage__Provider=Postgres
ENV ConnectionStrings__Postgres=Host=postgres;Database=harekat;Username=harekat;Password=harekat
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Harekat.Api.dll"]
