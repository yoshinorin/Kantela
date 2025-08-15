# Kantela

A Windows application built with WinUI 3 and the Windows App SDK.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
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
dotnet run --project src/Kantela.csproj

# Run with specific configuration
dotnet run --project src/Kantela.csproj --configuration Release
```

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

```bash
# Publish for Windows x64
dotnet publish src/Kantela.csproj -c Release -r win-x64 --self-contained

# Publish for Windows x86
dotnet publish src/Kantela.csproj -c Release -r win-x86 --self-contained

# Publish for Windows ARM64
dotnet publish src/Kantela.csproj -c Release -r win-arm64 --self-contained

# Publish as MSIX package
dotnet publish src/Kantela.csproj -f net8.0-windows10.0.19041.0 -c Release -p:Platform=x64 -p:PublishProfile=win-x64.pubxml
```

### Package Management

```bash
# List installed packages
dotnet list src/Kantela.csproj package

# Add a new package
dotnet add src/Kantela.csproj package <PackageName>

# Remove a package
dotnet remove src/Kantela.csproj package <PackageName>

# Update packages
dotnet restore --force
```


## License

TODO
