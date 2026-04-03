# 1. Build environment
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["APIGateway.csproj", "./"]
RUN dotnet restore "APIGateway.csproj"

# Copy the remaining source code and build
COPY . .
RUN dotnet publish "APIGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Environment variables for ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "APIGateway.dll"]
