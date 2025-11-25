# Build stage
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy project file
COPY DatabricksService.csproj .

# Restore dependencies
RUN dotnet restore

# Copy all source code
COPY . .

# Build the application
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS publish
WORKDIR /app
COPY --from=build /app/build .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables for non-interactive mode
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD dotnet DatabricksService.dll health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "DatabricksService.dll"]
