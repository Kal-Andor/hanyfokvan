# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy project file and restore as distinct layers
COPY ["HanyFokVan.Api/HanyFokVan.Api.csproj", "HanyFokVan.Api/"]
RUN dotnet restore "HanyFokVan.Api/HanyFokVan.Api.csproj"

# Copy source code and publish app
COPY . .
WORKDIR /source/HanyFokVan.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "HanyFokVan.Api.dll"]
