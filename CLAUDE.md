# CLAUDE.md

This file provides guidance to Claude Code when working with this project.

## Project Documentation

All project documentation is organized in the `docs/` directory:

- **[docs/SPECIFICATION.md](docs/SPECIFICATION.md)** - Functional specification (WHAT the system should do)
  - Project overview and key improvements over Helios
  - Core requirements (multi-alarm management, dimmer integration, brightness ramping, interruption detection)
  - MQTT integration behavior and message flow
  - Alarm execution behavior and state transitions
  - Non-functional requirements and success criteria
  - Security considerations

- **[docs/Architecture.md](docs/Architecture.md)** - Technical architecture (HOW the system is built)
  - Technology stack (.NET 10, MQTTnet, PostgreSQL, Hangfire)
  - System architecture and component responsibilities
  - MQTT service layer design and architecture principles
  - Configuration management and secrets handling
  - Data persistence and database schema
  - API surface design
  - Deployment, Docker multi-arch build, and testing strategy

- **[docs/MQTT-Reference.md](docs/MQTT-Reference.md)** - MQTT protocol reference (from Helios)
  - Topic configuration
  - Message formats (commands and status)
  - Minimum brightness threshold behavior
  - Debug commands for development

- **[docs/Roadmap.md](docs/Roadmap.md)** - Development roadmap
  - Phase 1: MVP (Backend Foundation)
  - Phase 2: Feature Completeness
  - Phase 3: Frontend Integration
  - Phase 4: Advanced Features

- **[docs/IMPLEMENTATION-MQTT-SERVICE-LAYER.md](docs/IMPLEMENTATION-MQTT-SERVICE-LAYER.md)** - MQTT Service Layer Implementation Record
  - Detailed implementation of all 4 MQTT service components
  - Configuration, models, interfaces, and implementations
  - Test coverage (28 unit tests passing)
  - API surface documentation
  - Build and deployment information

## Project Overview

**LumiRise** is an MQTT-based, multi-alarm dimmer controller application for automated wake-up lighting sequences. It serves as an improved implementation of the Helios proof-of-concept, with support for multiple independent alarms, enhanced interruption detection, and a modular backend architecture prepared for future frontend integration.

### Key Characteristics

- **Framework:** .NET 10 ASP.NET Core
- **Deployment:** Docker containers on ARM64 Raspberry Pi (with AMD64 development support)
- **Core Domain:** Multi-alarm wake-up light scheduling with MQTT dimmer device control
- **Architecture:** Backend-focused, API-ready for future frontend

## Quick Links

- Start with [docs/SPECIFICATION.md](docs/SPECIFICATION.md) for understanding requirements
- Refer to [docs/Architecture.md](docs/Architecture.md) for implementation details
- Check [docs/MQTT-Reference.md](docs/MQTT-Reference.md) for MQTT message formats and topic configuration
- See [docs/Roadmap.md](docs/Roadmap.md) for development phases and milestones
- See [src/CLAUDE.md](src/CLAUDE.md) for backend specific instructions and project layout

## Rules

- **NEVER use `git push --force`**. This is strictly forbidden, no exceptions.
- **Destructive operations always require explicit user approval** before execution.

## Development Context

When working on this project:

1. **Read the Specification first** to understand business requirements and what the system should do
2. **Check the Architecture document** for implementation patterns, technology choices, and technical constraints
3. **Follow the separated concerns:** Specification is about WHAT, Architecture is about HOW
4. **Consider the multi-architecture build:** Always test with both AMD64 (development) and ARM64 (deployment) builds
5. **MQTT Integration:** Keep compatibility with existing Helios message formats
6. **API-First Design:** Remember a frontend will be integrated in a future phase

## Document Status

**Last Updated:** 2026-01-31
**Specification Version:** 1.0
**Architecture Version:** 1.0
**MQTT Service Layer:** ✅ Complete (2026-01-31)
**Project Status:** Phase 1 Partially Complete - MQTT service layer implemented and tested
