# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Development Commands

```bash
# Build the solution
dotnet build

# Build using Cake build system
dotnet cake --target build

# Clean build artifacts
dotnet cake --target clean

# Run tests
dotnet cake --target test

# Create NuGet package
dotnet cake --target pack

# Install as global tool
dotnet tool install --global --add-source ./Artefacts TechDebtMaster.Cli --prerelease

# Uninstall global tool
dotnet tool uninstall TechDebtMaster.Cli -g

# Run CLI in development
dotnet run --project src/TechDebtMaster.Cli -- <command>
```

## Architecture Overview

TechDebtMaster is a .NET CLI application built with **Spectre.Console.Cli** and **Microsoft.SemanticKernel** for AI-powered technical debt analysis.

### Core Components

- **Commands/**: CLI command implementations using Spectre.Console.Cli command pattern
- **Services/**: Business logic layer with dependency injection
- **Program.cs**: Entry point with TypeRegistrar for DI and command configuration
- **Templates/**: Prompty template files for AI analysis

### Key Services

- **AnalysisService**: Orchestrates technical debt analysis through pluggable handlers
- **ConfigurationService**: JSON-based configuration stored in `~/.techdebtmaster/config.json`
- **RepositoryIndexService**: Integrates with repomix for repository content extraction
- **TechDebtStorageService**: Manages `.tdm` directory structure for analysis results
- **DialService**: Handles AI API communication via DIAL-compatible endpoints

### Command Structure

Commands are organized hierarchically:
- `analyze`: Repository analysis commands (`index`, `debt`, `show`, `view`, `status`)
- `config`: Configuration management (`show`, `set`)
- `dial`: AI model management (`models list`, `models set-default`, `limits`)
- `prompts`: Template management (`edit`, `restore-templates`, `set-default`)

### External Dependencies

- **repomix**: Must be installed globally (`npm install -g repomix`)
- **DIAL API**: Requires `DIAL_API_KEY` environment variable
- **AI Endpoints**: Configured via `config set` commands for Azure OpenAI-compatible services

### Analysis Architecture

Uses handler pattern with `IAnalysisHandler` implementations:
- **TechDebtAnalysisHandler**: AI-powered analysis using Semantic Kernel
- **PreviewHandler**: File preview generation

Analysis workflow:
1. repomix generates XML repository content
2. Changed files detected via hash comparison
3. AI analysis via Semantic Kernel with prompty templates
4. Results stored in repository-specific `.tdm` directory

### Configuration Hierarchy

1. Command-line parameters (highest priority)
2. Configuration file (`~/.techdebtmaster/config.json`)
3. Built-in defaults (lowest priority)

### Storage Structure

- Repository analysis: `<repo>/.tdm/` directory
- Configuration: `~/.techdebtmaster/config.json`
- Templates: User-customizable prompty files copied to config directory

## Development Notes

- Commands inherit from `AsyncCommand<TSettings>` or `Command<TSettings>`
- Add new services to `ServiceConfiguration.ConfigureServices()`
- Use Spectre.Console markup for consistent terminal UI
- Follow the handler pattern for extending analysis capabilities
- Repository paths and patterns are configuration-driven