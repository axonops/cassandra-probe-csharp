# Multi-stage build for Cassandra Probe C#

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY src/CassandraProbe.Core/*.csproj src/CassandraProbe.Core/
COPY src/CassandraProbe.Actions/*.csproj src/CassandraProbe.Actions/
COPY src/CassandraProbe.Services/*.csproj src/CassandraProbe.Services/
COPY src/CassandraProbe.Scheduling/*.csproj src/CassandraProbe.Scheduling/
COPY src/CassandraProbe.Logging/*.csproj src/CassandraProbe.Logging/
COPY src/CassandraProbe.Cli/*.csproj src/CassandraProbe.Cli/
COPY *.sln .

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/ src/

# Build application
RUN dotnet publish src/CassandraProbe.Cli/CassandraProbe.Cli.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS runtime
WORKDIR /app

# Install dependencies for network tools
RUN apk add --no-cache \
    ca-certificates \
    tzdata \
    icu-libs

# Copy published application
COPY --from=build /app/publish .

# Create non-root user
RUN addgroup -g 1000 -S cassandra && \
    adduser -u 1000 -S cassandra -G cassandra && \
    chown -R cassandra:cassandra /app

# Create directories for logs and config
RUN mkdir -p /app/logs /app/config && \
    chown -R cassandra:cassandra /app/logs /app/config

USER cassandra

# Set environment variables
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

# Entry point
ENTRYPOINT ["dotnet", "CassandraProbe.Cli.dll"]

# Default command (show help)
CMD ["--help"]