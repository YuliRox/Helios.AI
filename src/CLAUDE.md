# src/CLAUDE.md

Development context for the LumiRise backend source code.

## Solution Structure

```
src/
├── LumiRise.sln                    # Solution file
├── LumiRise.Api/                   # Main ASP.NET Core Web API project
├── LumiRise.Tests/                 # Unit tests (xUnit)
└── LumiRise.IntegrationTests/      # Integration tests (xUnit + Testcontainers)
```

## LumiRise.Api Project

### Target Framework
- .NET 10.0 (`net10.0`)
- Nullable reference types enabled
- Implicit usings enabled

### Installed NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| MQTTnet | 5.0.1.1416 | MQTT client for dimmer device communication |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.0 | PostgreSQL database provider for EF Core |
| Microsoft.EntityFrameworkCore.Design | 10.0.2 | EF Core tooling (migrations) |
| Hangfire.Core | 1.8.22 | Background job scheduling |
| Hangfire.AspNetCore | 1.8.22 | Hangfire ASP.NET Core integration |
| Hangfire.PostgreSql | 1.20.13 | Hangfire PostgreSQL storage |
| Serilog.AspNetCore | 10.0.0 | Structured logging |
| Serilog.Sinks.Console | 6.1.1 | Console log output |
| Serilog.Sinks.File | 7.0.0 | File log output |
| Swashbuckle.AspNetCore | 10.1.0 | OpenAPI/Swagger documentation |
| Microsoft.AspNetCore.OpenApi | 10.0.1 | OpenAPI support (template default) |

### Folder Structure

```
LumiRise.Api/
├── Controllers/          # API controllers (empty, to be implemented)
├── Services/
│   ├── Alarm/            # Alarm management services
│   └── Mqtt/             # MQTT communication services
├── Data/
│   ├── Entities/         # EF Core entity classes
│   └── Repositories/     # Repository pattern implementations
├── Models/
│   ├── Requests/         # API request DTOs
│   └── Responses/        # API response DTOs
├── Configuration/        # Configuration classes (strongly-typed options)
├── Properties/           # Launch settings
├── Program.cs            # Application entry point
├── appsettings.json      # Default configuration
└── appsettings.Development.json  # Development overrides
```

### Current State

The project contains the default .NET Web API template code with a sample `/weatherforecast` endpoint. This placeholder code should be replaced with actual LumiRise implementation.

**Program.cs** currently:
- Configures OpenAPI
- Has a sample WeatherForecast minimal API endpoint
- Uses HTTPS redirection

**Not yet configured:**
- Serilog logging
- Entity Framework Core DbContext
- Hangfire job scheduling
- MQTT services
- Custom controllers
- Configuration binding

## Test Projects

### LumiRise.Tests (Unit Tests)

| Package | Version |
|---------|---------|
| xUnit | 2.9.3 |
| Moq | 4.20.72 |
| AwesomeAssertions | 9.3.0 |
| coverlet.collector | 6.0.4 |

References `LumiRise.Api` project.

### LumiRise.IntegrationTests

| Package | Version |
|---------|---------|
| xUnit | 2.9.3 |
| Moq | 4.20.72 |
| AwesomeAssertions | 9.3.0 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.2 |
| Testcontainers.PostgreSql | 4.10.0 |
| Testcontainers.Mosquitto | 4.10.0 |
| coverlet.collector | 6.0.4 |

References `LumiRise.Api` project. Uses Testcontainers for spinning up real PostgreSQL and Mosquitto MQTT broker instances during tests.

## Dotnet Commands

```bash
# Restore dependencies
dotnet restore -v q --nologo

# Build solution
dotnet build -c Release -v q --nologo "$@"

# Run API project
dotnet run -v q --project LumiRise.Api

# Run tests
dotnet test -c Release -v q --logger "console;verbosity=quiet" "$@"

# Run specific test project
dotnet test -c Release -v q --logger "console;verbosity=quiet" LumiRise.Tests
dotnet test -c Release -v q --logger "console;verbosity=quiet" LumiRise.IntegrationTests
dotnet run --project LumiRise.Tests -c Release -v q
dotnet run --project LumiRise.IntegrationTests -c Release -v q

# Search for Nuget packages
dotnet package search --take 1 --source nuget.org "$@"
```

## Conventions

- **Resilience:** Use Polly (`Microsoft.Extensions.Resilience`) for the REST APIs retry and resilience logic. 

## Next Steps (Phase 1)

See `docs/Roadmap.md` for the full roadmap. Immediate next steps:

1. Configure Serilog in Program.cs
2. Set up EF Core DbContext and entities (Alarm, AlarmExecutionLog)
3. Create alarm CRUD API endpoints
4. Implement MQTT service layer
5. Add Hangfire for alarm scheduling
6. Create Docker and docker-compose files
