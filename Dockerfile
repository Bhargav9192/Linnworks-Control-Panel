# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

# Restore only the web project (this will restore referenced projects automatically)
RUN dotnet restore Linnworks.Host/Linnworks.Host.csproj

# Publish only the web project
RUN dotnet publish Linnworks.Host/Linnworks.Host.csproj -c Release -o /app/publish

# Final stage for running the app
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Set the environment variable to use HTTPS on port 7112
ENV ASPNETCORE_URLS=https://+:7112

# Expose port 7112 for HTTPS traffic
EXPOSE 7112

ENTRYPOINT ["dotnet", "Linnworks.Host.dll"]