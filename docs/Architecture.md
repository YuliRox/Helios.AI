# Architecture: MQTT-based Multi-Alarm Dimmer Controller

## Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| **Framework** | ASP.NET Core | 10.0 |
| **Language** | C# | 14 (latest) |
| **MQTT Client** | MQTTnet | 4.3+ (latest compatible) |
| **Scheduling** | Hangfire | 1.8+ |
| **Database** | PostgreSQL | 12+ |
| **ORM** | Entity Framework Core | 10.0 |
| **Logging** | Serilog | 8.0+ |
| **Dependency Injection** | Microsoft.Extensions.DependencyInjection | 10.0 |
| **Containerization** | Docker | Multi-arch (x64, ARM64v8) |
| **Runtime Base** | .NET 10 Official Runtime | Alpine or Debian variant |

---

## System Architecture

### High-Level Components

```
┌──────────────────────────────────────────────────────────────┐
│                     Frontend (Future)                        │
│              (Blazor/Web Interface - TBD)                    │
└────────────────────┬─────────────────────────────────────────┘
                     │ HTTP/REST API
┌────────────────────▼─────────────────────────────────────────┐
│            ASP.NET Core 10 Backend Service                   │
├──────────────────────────────────────────────────────────────┤
│  ┌────────────────────────────────────────────────────────┐  │
│  │ API Controllers (Alarm CRUD, Status, Controls)         │  │
│  └────────────────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ Alarm Service Layer                                    │  │
│  │ - AlarmManager (orchestrates multiple alarms)          │  │
│  │ - AlarmStateMachine (manages individual alarm states)  │  │
│  │ - AlarmScheduler (CRON-based trigger evaluation)       │  │
│  └────────────────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ MQTT Service Layer                                     │  │
│  │ - MqttConnectionManager (connection/reconnection)      │  │
│  │ - DimmerCommandPublisher (command execution)           │  │
│  │ - DimmerStateMonitor (status observation & caching)    │  │
│  │ - InterruptionDetector (manual operation detection)    │  │
│  └────────────────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ Data Persistence                                       │  │
│  │ - AlarmRepository (PostgreSQL)                         │  │
│  │ - AlarmExecutionLog (execution history)                │  │
│  └────────────────────────────────────────────────────────┘  │
└─────────────────────────┬────────────────────────────────────┘
                          │ MQTT
┌─────────────────────────▼────────────────────────────────────┐
│              MQTT Broker (mosquitto/etc)                     │
└─────────────────────────┬────────────────────────────────────┘
                          │ MQTT
┌─────────────────────────▼────────────────────────────────────┐
│          MQTT Dimmer Device (Tasmota/Sonoff/etc)             │
└──────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| **AlarmManager** | Manages all active and scheduled alarms; orchestrates state transitions; routes commands to appropriate alarms |
| **AlarmStateMachine** | Encapsulates state logic for a single alarm (Idle → Running → Paused → Complete/Cancelled) |
| **AlarmScheduler** | Evaluates CRON triggers; determines which alarms should be active at any given time |
| **MqttConnectionManager** | Establishes/maintains MQTT connection; handles reconnection logic with exponential backoff |
| **DimmerCommandPublisher** | Publishes commands to MQTT topics; implements brightness ramping logic |
| **DimmerStateMonitor** | Subscribes to status topics; caches latest state; notifies subscribers of changes |
| **InterruptionDetector** | Compares expected vs. actual dimmer state; detects manual user intervention |

---

## MQTT Service Layer

### Architecture Principles

1. **Separation of Concerns:**
   - Connection management isolated from command/status operations
   - State observation decoupled from command execution
   - Interrupt detection as distinct concern

2. **Reactive Pattern:**
   - `DimmerStateMonitor` publishes state changes via `IObservable<DimmerState>`
   - Components subscribe to state changes rather than polling
   - Enables real-time interruption detection

3. **Resilience:**
   - Automatic reconnection with exponential backoff
   - Command queuing during disconnection
   - Graceful degradation on MQTT unavailability

### Service Layer Components

The MQTT service layer is implemented as a set of loosely-coupled, interface-driven services:

- **MqttConnectionManager:** Manages broker connection lifecycle with reconnection logic and connection state notifications
- **DimmerStateMonitor:** Observes and caches current dimmer state; publishes state changes reactively
- **DimmerCommandPublisher:** Executes dimmer commands (power, brightness) via MQTT publication
- **InterruptionDetector:** Compares expected vs. actual dimmer state to detect manual user intervention

---

## Configuration Management

### Configuration Sources (Priority Order)

1. **Environment Variables** (highest priority)
2. **appsettings.json** (default values)
3. **appsettings.{Environment}.json** (environment-specific overrides)

### Required Environment Variables (Production)

The following environment variable categories must be configured:

- **Database Connection:** PostgreSQL hostname, database name, username, password
- **MQTT Broker:** Server hostname/IP, port, authentication credentials, client identifier
- **Application:** Logging level, ASP.NET Core environment designation
- **Feature Settings:** Optional feature flags and configuration overrides

### MQTT Topic & Feature Configuration

The application configuration file specifies:

- **MQTT Topics:** Command and status topics for power control and brightness adjustment (configurable per deployment)
- **Connection Parameters:** Keep-alive interval, reconnection delay, and maximum reconnection attempts
- **Alarm Defaults:** Default ramp duration, minimum brightness threshold, MQTT status timeout, and power-on confirmation timeout

### Secrets Management

- **Sensitive Data:** Username, password, connection strings
- **Storage:** Environment variables only (never in code or JSON files)
- **Development:** Use .env file with dotnet user-secrets (not committed)
- **Production:** Docker secrets or host environment

---

## Data Persistence & Schema

### Database Schema

The persistence layer requires two main entities:

**Alarms Entity:**
Stores alarm definitions with trigger time, enabled status, days of week, brightness settings (start, target), ramp duration, and timezone configuration. Each alarm has a unique identifier and metadata for creation/update tracking.

**Alarm Execution Log Entity:**
Records execution history for each alarm trigger, including scheduled time, actual start time, completion time, execution status (Scheduled, Running, Completed, Interrupted, Failed), interruption reason if applicable, final brightness level, and any error messages.

### Persistence Layer

- PostgreSQL for data storage with schema migrations
- Object-relational mapping via modern ORM framework
- Repository pattern for data access abstraction
  - Repository for alarm CRUD operations
  - Repository for execution log operations
- Database connection configuration via environment variables

---

## API Surface (for Future Frontend)

### Alarm Management Endpoints

The backend exposes RESTful operations for alarm lifecycle management:

- **List all alarms** with current state, enabled/disabled status, and execution metadata
- **Create new alarm** with name, trigger time, days of week, brightness settings
- **Retrieve single alarm** details by identifier
- **Update alarm** properties (name, schedule, brightness curve)
- **Delete alarm** by identifier
- **Enable/disable individual alarms** via state toggle operations

### Control Endpoints

The backend provides operational control endpoints:

- **Start/stop individual alarms** for manual triggering or cancellation
- **Query system status** including MQTT connection state and active dimmer devices
- **Query dimmer state** to retrieve current power status and brightness level
- **Send commands to dimmer** for brightness adjustment or power control

### Status & Diagnostics Endpoints

The backend exposes visibility into execution history and system health:

- **Retrieve execution history** for a specific alarm with pagination support, including completion status and interruption reasons
- **Health check endpoint** to verify service readiness, database connectivity, and MQTT broker status

---

## Deployment & Build Configuration

### Docker Multi-Architecture Build

The build process supports two target platforms:
- **linux/amd64:** For development and testing on AMD64 machines
- **linux/arm64/v8:** For production deployment on Raspberry Pi ARM64

Multi-platform builds use Docker buildx to compile and package the application for both architectures in a single build operation, enabling consistent deployments across development and production environments.

### Dockerfile Strategy

The containerization uses a multi-stage build approach:
- **Builder stage:** .NET SDK for compiling the application with cross-architecture support
- **Runtime stage:** Minimal .NET runtime image for reduced footprint

This approach optimizes container size while maintaining flexibility to compile for different architectures.

### Docker Compose (Development)

A compose configuration provides a complete local development environment with:
- PostgreSQL database service with persistent storage
- MQTT broker service for message testing
- Application service with automatic rebuild capability and port exposure
- Service interdependencies to ensure startup order

### Production Deployment

- **Container Registry:** private registry
- **Orchestration:** None (docker compose)
- **Database:** External PostgreSQL (self-hosted)
- **MQTT Broker:** External (can be on same Pi or remote)
- **Logging:** Serilog to file or centralized logging service

---

## Testing Strategy

### Unit Testing
- Alarm state machine transitions
- Brightness ramp calculations
- Interruption detection logic
- Configuration parsing

### Integration Testing
- MQTT publish/subscribe flow
- Database persistence (alarm CRUD, execution logging)
- API endpoint functionality
- Multi-alarm concurrency

### System Testing
- End-to-end wake-up sequence (with mock MQTT broker)
- Manual interruption scenario
- Connection loss recovery
- Multi-arch container builds and execution

### Performance Testing
- Ramp step latency (< 100ms per step)
- API response time (< 200ms)
- Database query performance (indexed appropriately)

---

## Document Metadata

**Version:** 1.0
**Status:** Architecture Reference
**Last Updated:** 2026-01-31
**Author:** Claude Code
**Related Documents:** SPECIFICATION.md
