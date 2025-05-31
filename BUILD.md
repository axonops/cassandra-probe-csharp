# Build Instructions

This document provides comprehensive build instructions for the Cassandra Probe C# project across different platforms.

## Prerequisites

- **.NET 9.0 SDK** or later
- **Git** (for cloning the repository)
- **Container Runtime** (Docker or Podman) - Optional, for testing

## Quick Build

```bash
# Clone the repository
git clone https://github.com/axonops/cassandra-probe-csharp.git
cd cassandra-probe-csharp

# Build all projects
dotnet build

# Run tests
dotnet test

# Build release executable for current platform
dotnet publish src/CassandraProbe.Cli -c Release --self-contained -p:PublishSingleFile=true
```

## Platform-Specific Instructions

### Windows

#### Install .NET SDK

1. Download the .NET 9.0 SDK from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
2. Run the installer
3. Verify installation:
   ```powershell
   dotnet --version
   ```

#### Build Steps

```powershell
# Clone repository
git clone https://github.com/axonops/cassandra-probe-csharp.git
cd cassandra-probe-csharp

# Restore dependencies
dotnet restore

# Build debug version
dotnet build

# Run tests
dotnet test

# Build release executable
dotnet publish src/CassandraProbe.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/windows

# The executable will be at: publish\windows\cassandra-probe.exe
```

### macOS

#### Install .NET SDK

Using Homebrew:
```bash
brew install --cask dotnet-sdk
```

Or download directly from [Microsoft](https://dotnet.microsoft.com/download).

#### Build Steps

```bash
# Clone repository
git clone https://github.com/axonops/cassandra-probe-csharp.git
cd cassandra-probe-csharp

# Build for Intel Mac
dotnet publish src/CassandraProbe.Cli -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o publish/osx-x64

# Build for Apple Silicon (M1/M2)
dotnet publish src/CassandraProbe.Cli -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o publish/osx-arm64

# Make executable
chmod +x publish/osx-*/cassandra-probe
```

### Linux

#### Debian/Ubuntu

Install .NET SDK:
```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-9.0
```

Build:
```bash
# Clone and build
git clone https://github.com/axonops/cassandra-probe-csharp.git
cd cassandra-probe-csharp

# Build for Linux x64
dotnet publish src/CassandraProbe.Cli -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/linux-x64

# Build for ARM64 (Raspberry Pi, AWS Graviton, etc.)
dotnet publish src/CassandraProbe.Cli -c Release -r linux-arm64 --self-contained -p:PublishSingleFile=true -o publish/linux-arm64

# Make executable
chmod +x publish/linux-*/cassandra-probe
```

#### RHEL/CentOS/Fedora

Install .NET SDK:
```bash
# For RHEL 9/CentOS Stream 9/Fedora 38+
sudo dnf install dotnet-sdk-9.0

# For older versions, add Microsoft repository first
sudo rpm -Uvh https://packages.microsoft.com/config/rhel/9/packages-microsoft-prod.rpm
sudo dnf install dotnet-sdk-9.0
```

Build steps are the same as Debian/Ubuntu.

#### Alpine Linux (musl)

```bash
# Install .NET SDK
apk add dotnet9-sdk

# Build for Alpine
dotnet publish src/CassandraProbe.Cli -c Release -r linux-musl-x64 --self-contained -p:PublishSingleFile=true -o publish/alpine
```

## Build Configurations

### Debug Build

For development and debugging:
```bash
dotnet build -c Debug
```

### Release Build

For production use:
```bash
dotnet build -c Release
```

### Self-Contained vs Framework-Dependent

#### Self-Contained (Recommended)
Includes .NET runtime - no installation required on target machine:
```bash
dotnet publish -c Release -r [RID] --self-contained -p:PublishSingleFile=true
```

#### Framework-Dependent
Smaller file size but requires .NET runtime on target machine:
```bash
dotnet publish -c Release -r [RID] --self-contained false
```

### Runtime Identifiers (RID)

| Platform | RID |
|----------|-----|
| Windows x64 | win-x64 |
| Windows x86 | win-x86 |
| Windows ARM64 | win-arm64 |
| macOS x64 (Intel) | osx-x64 |
| macOS ARM64 (M1/M2) | osx-arm64 |
| Linux x64 | linux-x64 |
| Linux ARM | linux-arm |
| Linux ARM64 | linux-arm64 |
| Alpine Linux x64 | linux-musl-x64 |
| Alpine Linux ARM64 | linux-musl-arm64 |

## Advanced Build Options

### Trimming (Reduce Size)

```bash
dotnet publish -c Release -r [RID] --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
```

### ReadyToRun (Faster Startup)

```bash
dotnet publish -c Release -r [RID] --self-contained -p:PublishSingleFile=true -p:PublishReadyToRun=true
```

### Native AOT (Experimental)

For smallest size and fastest startup:
```bash
dotnet publish -c Release -r [RID] -p:PublishAot=true
```

## Docker Build

Build in Docker container:

```dockerfile
# Create Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/CassandraProbe.Cli -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["./cassandra-probe"]
```

Build:
```bash
docker build -t cassandra-probe .
```

## Testing the Build

After building, test the executable:

```bash
# Show help
./cassandra-probe --help

# Test with local Cassandra
./cassandra-probe -cp localhost:9042

# Run all unit tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Troubleshooting

### Common Issues

1. **"dotnet: command not found"**
   - Ensure .NET SDK is installed and in PATH
   - Restart terminal after installation

2. **"Unable to find package"**
   - Run `dotnet restore` to restore NuGet packages
   - Check internet connection

3. **"Permission denied" on Linux/macOS**
   - Make executable: `chmod +x cassandra-probe`

4. **Large executable size**
   - Use trimming: add `-p:PublishTrimmed=true`
   - Consider framework-dependent deployment

5. **Build errors with Cassandra driver**
   - Ensure you're using .NET 9.0 or later
   - Clear NuGet cache: `dotnet nuget locals all --clear`

## Continuous Integration

For CI/CD pipelines:

```yaml
# GitHub Actions example
- name: Setup .NET
  uses: actions/setup-dotnet@v3
  with:
    dotnet-version: 9.0.x

- name: Build
  run: dotnet build -c Release

- name: Test
  run: dotnet test --no-build -c Release

- name: Publish
  run: |
    dotnet publish src/CassandraProbe.Cli -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./artifacts/linux-x64
    dotnet publish src/CassandraProbe.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./artifacts/win-x64
    dotnet publish src/CassandraProbe.Cli -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o ./artifacts/osx-x64
```

## Development Tips

1. **Use Visual Studio Code** with C# extension for cross-platform development
2. **Use JetBrains Rider** for advanced refactoring and debugging
3. **Run tests frequently**: `dotnet watch test`
4. **Check code coverage**: Use coverlet or similar tools
5. **Profile performance**: Use `dotnet-trace` or `PerfView`

## Contributing

When contributing, ensure:
- All tests pass: `dotnet test`
- Code follows conventions: `dotnet format`
- No build warnings in Release mode
- Documentation is updated