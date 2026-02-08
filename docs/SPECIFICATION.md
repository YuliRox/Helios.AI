# Project Specification: MQTT-based Multi-Alarm Dimmer Controller

## 1. Project Overview

**Project Name:** LumiRise

**Purpose:** A containerized, scalable alarm clock application that automates wake-up lighting sequences via MQTT-controlled dimmer devices. Unlike the proof-of-concept Helios implementation, this system supports multiple sequential alarm entries with enhanced interruption detection and a modular backend architecture prepared for future frontend integration.

**Target Deployment:** Docker containers on ARM64 Raspberry Pi (with cross-compilation support for AMD64 development machines)

**Key Improvement Over Helios:**
- Multi-alarm support (multiple wake-up entries scheduled independently)
- Redesigned MQTT service layer with cleaner abstraction
- State machine-based alarm execution (not just job-based scheduling)
- Prepared API surface for frontend integration
- No direct UI dependency in backend

---

## 2. Core Requirements

### 2.1 Multi-Alarm Management

- **Multiple Wake-Up Entries:** Support unlimited number of independently scheduled alarms
  - Each alarm has: Name, enabled/disabled status, time (HH:MM), days of week, brightness curve
  - Alarms execute in isolation; no dependencies between alarms
  - Manual operation of dimmer during one alarm does not affect others
  - Alarms can only happen sequentially - executing Alarms at the same time on the same device does not make sense
  - Alarms are checked for overlap during scheduling. The time needed by the brightness ramp is added to an existing alarms trigger time, this is known as the alarm execution time. Other alarms may not be scheduled during any alarms execution time.

- **Alarm Lifecycle:**
  ```
  Scheduled → Triggered → Running → Complete/Interrupted → Scheduled
  ```

- **Alarm State Persistence:**
  - All alarm definitions stored in persistance layer
  - Execution history logged (start time, completion status, interruption reason)
  - State survives application restart

### 2.2 Dimmer Integration

- **MQTT Topic Structure:** Maintain compatibility with existing Helios message format
  - Separate topics for power on/off commands and status
  - Separate topics for brightness percentage commands and status
  - Topics configurable via environment variables for flexibility

- **Dimmer Operations:**
  - Turn on/off via JSON-formatted MQTT message
  - Set brightness 0-100 via integer payload message
  - Respect hardware minimum brightness threshold (default: 20% to avoid flickering)

### 2.3 Brightness Ramping

- **Wake-Up Curve:**
  - Start at minimum brightness percentage (e.g., 20%)
  - Gradually increment to target brightness (typically 100%)
  - Configurable ramp duration per alarm (default: 30-45 minutes)

- **Flexible Curves:**
  - Linear progression (current implementation, default)
  - Logarithmic/cubic easing
  - Custom waypoint curves

### 2.4 Interruption Detection

- **Manual Override Detection:**
  - continuously cache latest MQTT status
  - During brightness ramp, compare expected vs. actual state
  - If dimmer state diverges from expected value, mark alarm as interrupted
  - Gracefully stop ongoing ramp; preserve other scheduled alarms

- **Interruption Triggers:**
  - User manually changes brightness via physical device/remote
  - User turns dimmer on/off
  - Dimmer device loses connection

---

## 3. MQTT Integration Behavior

### 3.1 Message Flow

**Successful Wake-Up Sequence:**
During a normal wake-up execution, the system triggers an alarm, publishes a power-on command to the MQTT broker, receives device confirmation, then gradually increments brightness while monitoring for interruption. At each brightness step, the system waits for device acknowledgment and compares the actual state against expected state before proceeding to the next step.

**Interrupted Wake-Up (Manual Override):**
When a user manually adjusts the dimmer during an active wake-up ramp, the device publishes a state change that no longer matches the system's expected brightness level. The application detects this interruption and gracefully stops the ongoing ramp.

---

## 4. Alarm Execution Behavior

### 4.1 Alarm States

```
States: Idle, Triggered, Running, Paused, Completed, Interrupted, Failed

Idle ──[Scheduler Trigger]──> Triggered ──[Start]──> Running
                                 │                      │
                                 │                      ├──[Manual Override]──> Interrupted ──[Restart Schedule]──> Idle
                                 │                      │
                                 │                      ├──[Complete]──> Completed ──[Reset]──> Idle
                                 │                      │
                                 │                      └──[Error]──> Failed ──[Reset]──> Idle
                                 │
                                 └──[Cancel]──> Idle

Idle ──[User Interaction]──> Paused

Paused ──[User Interaction]──> Idle
```

### 4.2 Concurrency & Multi-Alarm Behavior

- **Independent Execution:** Multiple alarms cannot execute simultaneously. Multiple Alarms can be executed sequentially.
- **No Parallel Alarms:** If the user tries to configure multiple alarms executing during the same timeframe, the application considers this as an error. New alarms are checked for overlaps with existing alarms.
- **State Isolation:** Each alarm maintains independent state; no cross-talk
- **Mutex on Commands:** Only one brightness command published at a time (queue if multiple alarms active)

---

## 5. Non-Functional Requirements

| Requirement | Target | Notes |
|---|---|---|
| **Availability** | 99.5% uptime | Graceful degradation on MQTT loss |
| **Latency** | < 100ms command-to-dimmer | Including MQTT broker latency |
| **Throughput** | 1000 API requests/min | Per-instance capacity |
| **Startup Time** | < 10s | Cold start with DB migration |
| **Memory (ARM64)** | < 200MB | Raspberry Pi 4 minimum |
| **Log Retention** | 30 days minimum | Configurable retention policy |
| **Recovery Time (RTO)** | < 5 minutes | After container restart |
| **Data Loss (RPO)** | < 1 minute | PostgreSQL transaction safety |

---

## 6. Security Considerations

- **MQTT Authentication:** Username/password required (no anonymous access)
- **Network Isolation:** MQTT broker on trusted LAN only
- **Database:** PostgreSQL user with minimal required privileges
- **API Authentication:** (Future) JWT or API key (prepare infrastructure now)
- **Container Security:** Run as non-root user; read-only root filesystem where possible
- **Secrets:** Never hardcoded; environment variables or Docker secrets only
- **Logging:** No sensitive data (passwords, keys) in logs

---

## 7. Success Criteria

-  Multiple alarms can be scheduled and execute independently
-  Manual dimmer operation interrupts ongoing alarm gracefully
-  Alarm state survives application restart
-  MQTT commands follow existing message format
-  Container builds for both amd64 and arm64v8
-  API surface ready for future frontend integration
-  No UI included; backend only
-  .NET 10 with modern C# language features
-  < 200MB memory footprint on ARM64
-  Comprehensive logging for debugging

---

## Document Metadata

**Version:** 1.0
**Status:** Specification (Ready for Review)
**Last Updated:** 2026-01-31
**Author:** Claude Code
**Next Steps:** Architecture review → Development kickoff

