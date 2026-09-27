# Kantela

A Windows application built with WinUI 3 and the Windows App SDK.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- Windows 10 version 1903 (build 18362) or later

## Getting Started

### Restore Dependencies

```bash
dotnet restore
```

### Build the Application

```bash
# Build for the current platform
dotnet build

# Build for specific platform
dotnet build -p:Platform=x64
dotnet build -p:Platform=x86
dotnet build -p:Platform=ARM64
```

### Run the Application

```bash
# Run in Debug mode
dotnet run --project src/Kantela/Kantela.csproj

# Run with specific configuration
dotnet run --project src/Kantela/Kantela.csproj --configuration Release
```

### Run Tests

```bash
dotnet test --project tests/Kantela.Core.Tests/Kantela.Core.Tests.csproj
```

## Project Structure

```
src/Kantela/              WinUI 3 app
src/Kantela.Core/         UI-independent logic (models, data, services, view models)
tests/Kantela.Core.Tests/ Tests for Kantela.Core
docs/                     Design documents
```

See [docs/design.md](docs/design.md) for the design.

## Development Commands

### Build Commands

```bash
# Clean build artifacts
dotnet clean

# Rebuild the solution
dotnet rebuild

# Build in Release configuration
dotnet build --configuration Release
```

### Code Formatting

```bash
# Format code according to .editorconfig
dotnet format

# Format code and verify formatting
dotnet format --verify-no-changes

# Format only whitespace issues
dotnet format whitespace

# Format code style issues
dotnet format style
```

### Publishing

Kantela is an unpackaged app that bundles the .NET runtime and the Windows App SDK runtime.
Copy the output folder anywhere and run `Kantela.exe`. No installation or signing is required.

```bash
# Publish for Windows x64
dotnet publish src/Kantela/Kantela.csproj -c Release -r win-x64 -p:Platform=x64

# Publish for Windows x86
dotnet publish src/Kantela/Kantela.csproj -c Release -r win-x86 -p:Platform=x86

# Publish for Windows ARM64
dotnet publish src/Kantela/Kantela.csproj -c Release -r win-arm64 -p:Platform=ARM64
```

Output: `src/Kantela/bin/Release/net10.0-windows10.0.19041.0/<runtime>/publish/`

### Package Management

```bash
# List installed packages
dotnet list src/Kantela/Kantela.csproj package

# Add a new package
dotnet add src/Kantela/Kantela.csproj package <PackageName>

# Remove a package
dotnet remove src/Kantela/Kantela.csproj package <PackageName>

# Update packages
dotnet restore --force
```


## License

TODO
