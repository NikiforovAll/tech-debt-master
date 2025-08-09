# TechDebtMaster

A CLI tool for analyzing and managing technical debt in software repositories using AI-powered analysis with Microsoft Semantic Kernel.

## Overview

TechDebtMaster is a command-line application that helps developers identify, analyze, and manage technical debt in their codebases. It leverages the power of AI through Microsoft Semantic Kernel and integrates with `repomix` to provide comprehensive repository analysis.

## Features

- **Repository Indexing**: Index repositories using `repomix` to extract file summaries and structure
- **AI-Powered Analysis**: Analyze technical debt using Azure OpenAI models
- **Beautiful CLI Interface**: Modern command-line interface powered by Spectre.Console
- **Extensible Architecture**: Built with dependency injection and modular design

## Prerequisites

- .NET 9.0 SDK
- [repomix](https://github.com/yamadashy/repomix) installed and available in PATH
- Azure OpenAI API access (via EPAM AI Proxy or direct Azure OpenAI)

## Installation

`dotnet cake --target pack`

`dotnet tool install --global --add-source ./Artefacts TechDebtMaster.Cli --prerelease`

`dotnet tool uninstall TechDebtMaster.Cli -g`

### Prerequisites Setup

1. **Install .NET 9.0 SDK**
   ```bash
   # Download from https://dotnet.microsoft.com/download/dotnet/9.0
   ```

2. **Install repomix**
   ```bash
   npm install -g repomix
   ```

### Build from Source

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd tech-debt-master
   ```

2. **Build the solution**
   ```bash
   dotnet build
   ```

3. **Build using Cake (optional)**
   ```bash
   dotnet cake --target build
   ```
## Usage

### Available Commands

#### Index Command
Index a repository to extract file summaries and prepare for analysis:

```bash
dotnet run --project src/TechDebtMaster.Cli -- index <repository-path>
```

**Examples:**
```bash
# Index a local repository
dotnet run --project src/TechDebtMaster.Cli -- index "C:\my-project"

# Index a repository on Linux/macOS
dotnet run --project src/TechDebtMaster.Cli -- index "/home/user/my-project"
```

#### Analyze Command
Analyze a repository for technical debt (currently shows mock data):

```bash
dotnet run --project src/TechDebtMaster.Cli -- analyze <repository-path>
```

## Acknowledgments

- [Spectre.Console](https://spectreconsole.net/) for the beautiful CLI framework
- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) for AI orchestration
- [repomix](https://github.com/yamadashy/repomix) for repository packaging
