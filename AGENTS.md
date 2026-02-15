# AGENTS.md

This file is the startup contract for coding agents in this repository.

## Goal

Keep startup context loading fast and consistent. Do not crawl the whole repo first.

## Read These Files First

Read in this order, and stop once you have enough context for the task.

1. `docs/project-context.md` - current implementation snapshot and known gaps.
2. `src/LumiRise.Api/Program.cs` - DI wiring, middleware, DB migration, Hangfire setup.
3. `src/LumiRise.Api/Controllers/AlarmsController.cs` - alarm API shape and request validation.
4. `src/LumiRise.Api/Data/Entities/AlarmScheduleEntity.cs` - persisted alarm model and value clamps.
5. `src/LumiRise.Api/Data/LumiRiseDbContext.cs` - table mapping, constraints, and indexes.
6. `docker-compose.yml` - local runtime topology (API + Postgres + Mosquitto + mock dimmer).
7. `src/LumiRise.Api/appsettings.json` - default Postgres/MQTT/scheduler settings.

If task scope is clear after these files, do not read more.

## Task-Specific Read Paths

For scheduling and Hangfire behavior:

1. `src/LumiRise.Api/Services/Alarm/Implementation/AlarmRecurringJobSynchronizer.cs`
2. `src/LumiRise.Api/Services/Alarm/Implementation/AlarmRecurringJobSyncHostedService.cs`
3. `src/LumiRise.Api/Services/Alarm/Implementation/AlarmExecutionJob.cs`
4. `src/LumiRise.IntegrationTests/Services/Alarm/HangfireAlarmSchedulingIntegrationTests.cs`

For alarm lifecycle/ramping behavior:

1. `src/LumiRise.Api/Services/Alarm/Implementation/AlarmStateMachine.cs`
2. `src/LumiRise.Api/Services/Alarm/Implementation/AlarmStateMachineFactory.cs`
3. `src/LumiRise.Tests/Services/Alarm/AlarmStateMachineTests.cs`

For MQTT behavior:

1. `src/LumiRise.Api/Services/Mqtt/Implementation/MqttConnectionManager.cs`
2. `src/LumiRise.Api/Services/Mqtt/Implementation/DimmerStateMonitor.cs`
3. `src/LumiRise.Api/Services/Mqtt/Implementation/DimmerCommandPublisher.cs`
4. `src/LumiRise.Api/Services/Mqtt/Implementation/InterruptionDetector.cs`
5. `src/LumiRise.IntegrationTests/Services/Mqtt/MqttIntegrationTests.cs`
6. `src/LumiRise.IntegrationTests/MockDimmerDevice.cs`

For API endpoint behavior:

1. `src/LumiRise.Api/Models/Alarms/AlarmUpsertRequest.cs`
2. `src/LumiRise.Api/Models/Alarms/AlarmResponse.cs`
3. `src/LumiRise.IntegrationTests/Services/Alarm/AlarmApiIntegrationTests.cs`

## Commands

Run from repo root unless noted.

- Build API only: `dotnet build src/LumiRise.Api/LumiRise.Api.csproj --nologo -v q`
- Build integration tests only: `dotnet build src/LumiRise.IntegrationTests/LumiRise.IntegrationTests.csproj --nologo -v q`
- Build full solution (stable): `dotnet build src/LumiRise.sln --nologo -m:1 -v q`
- Run unit tests: `dotnet test src/LumiRise.Tests/LumiRise.Tests.csproj --nologo -m:1 -nodeReuse:false -v q`
- Start local stack: `docker compose up -d`
- API Swagger: `http://localhost:8080/swagger`
- Hangfire dashboard: `http://localhost:8080/hangfire`

## Notes

- `docs/project-context.md` is the preferred current-state document.
- `docs/SPECIFICATION.md` and `docs/Architecture.md` are useful for intent, but verify behavior in code before changing logic.
- Keep `docs/project-context.md` updated when architecture or workflow changes materially.
