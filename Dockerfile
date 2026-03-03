FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

# Restore only the web project (this will restore referenced projects automatically)
RUN dotnet restore Linnworks.Host/Linnworks.Host.csproj

# Publish only the web project
RUN dotnet publish Linnworks.Host/Linnworks.Host.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Linnworks.Host.dll"]