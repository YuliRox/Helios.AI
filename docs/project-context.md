# Project Context

Last updated: 2026-02-15

## What This Project Is

LumiRise is a .NET 10 backend that schedules wake-up light alarms and controls an MQTT dimmer device.

Primary responsibilities:

- Persist alarm schedules in PostgreSQL.
- Synchronize enabled alarms into Hangfire recurring jobs.
- Execute alarm ramps by publishing MQTT power/brightness commands.
- Detect manual interruption by comparing expected vs actual dimmer state.

## Current Runtime Shape

- API project: `src/LumiRise.Api`
- Unit tests: `src/LumiRise.Tests`
- Integration tests: `src/LumiRise.IntegrationTests`
- Mock dimmer app: `src/LumiRise.MockDimmerDevice`
- Local runtime stack: `docker-compose.yml`

Boot path at runtime:

1. `Program.cs` registers DbContext, Hangfire, MQTT services, alarm services.
2. EF Core migrations are applied at startup (`Database.MigrateAsync()`).
3. `AlarmRecurringJobSyncHostedService` performs a one-time sync of DB alarms into Hangfire recurring jobs at startup.
4. Hangfire triggers `AlarmExecutionJob` for each scheduled alarm.
5. Job creates `AlarmStateMachine`, connects MQTT, starts monitoring, runs ramp, then disconnects.

## What Is Implemented

- Alarm CRUD endpoints in `AlarmsController` using weekly schedule fields (`daysOfWeek` + `time`) plus `rampMode`.
- Ramp profile CRUD endpoints in `RampsController` (`/api/ramps`) backed by `ramp_profiles`.
- Alarm schedules reference ramp profiles by FK (`RampProfileId`) instead of storing brightness/duration directly.
- `rampMode` resolves to the matching ramp profile row; startup ensures a `default` profile exists (20% -> 100% over 1800s).
- Alarm timezone is global via app settings, not part of alarm payload.
- Alarm schedule persistence with EF Core migrations.
- Hangfire recurring-job sync from database.
- Alarm state machine with transition table and execution pipeline.
- MQTT connection manager with reconnect loop and queued commands.
- Dimmer state monitor, command publisher, and interruption detector.
- Integration tests for alarm API, Hangfire scheduling, and MQTT components.

## Important Gaps / Caveats

- Overlap prevention is enforced for enabled weekly alarms that share weekdays; disabled alarms are excluded until enabled.
- Overlap prevention compares alarm windows using each alarm's selected ramp profile duration.
- Hangfire dashboard authorization is currently allow-all (`AllowAllHangfireDashboardAuthorizationFilter`).
- `docs/Architecture.md` and `docs/Roadmap.md` contain intent and history; verify against code before assuming current behavior.
- In this environment, `dotnet build src/LumiRise.sln` is flaky with default parallelism and may exit non-zero with no diagnostics; `dotnet build src/LumiRise.sln -m:1` succeeds.

## Read Order For Fast Orientation

Start here before any deep repo scan:

1. `AGENTS.md`
2. `src/LumiRise.Api/Program.cs`
3. `src/LumiRise.Api/Controllers/AlarmsController.cs`
4. `src/LumiRise.Api/Data/Entities/AlarmScheduleEntity.cs`
5. `src/LumiRise.Api/Data/LumiRiseDbContext.cs`
6. `docker-compose.yml`
7. `src/LumiRise.Api/appsettings.json`

Then choose one focused track:

- Scheduling: `AlarmRecurringJobSynchronizer.cs` -> `AlarmExecutionJob.cs` -> `HangfireAlarmSchedulingIntegrationTests.cs`
- Alarm execution: `AlarmStateMachine.cs` -> `AlarmStateMachineTests.cs`
- MQTT: `MqttConnectionManager.cs` -> `DimmerStateMonitor.cs` -> `DimmerCommandPublisher.cs` -> `InterruptionDetector.cs` -> `MqttIntegrationTests.cs`

## Working Commands

Run from repo root unless noted:

- Build API: `dotnet build src/LumiRise.Api/LumiRise.Api.csproj --nologo -v q`
- Build integration tests: `dotnet build src/LumiRise.IntegrationTests/LumiRise.IntegrationTests.csproj --nologo -v q`
- Build full solution (stable): `dotnet build src/LumiRise.sln --nologo -m:1 -v q`
- Run unit tests: `dotnet test src/LumiRise.Tests/LumiRise.Tests.csproj --nologo -m:1 -nodeReuse:false -v q`
- Start local dependencies + API + mock dimmer: `docker compose up -d`

## Configuration Pointers

- Postgres connection string: `ConnectionStrings:Postgres`
- MQTT options section: `Mqtt`
- Alarm settings section: `AlarmSettings` (`TimeZoneId`)
- Alarm synchronization is startup-only plus immediate sync on alarm create/update/delete.
- Local defaults are in `src/LumiRise.Api/appsettings.json`
- Compose overrides are in `docker-compose.yml`

## Keep This File Fresh

Update this file when one of these changes:

- startup/runtime wiring (`Program.cs`, hosted services, composition)
- API contract for alarms
- scheduling semantics (recurring sync, execution rules)
- MQTT topic semantics or payload format
- recommended first-read list
