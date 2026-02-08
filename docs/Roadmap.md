# Development Roadmap

## Phase 0: Setup
- [x] Create a bash script containing all cli commands to scaffold the structure for the backend projekt
- [x] Verify Setup Script with User
- [x] Execute setup script

## Phase 1: MVP (Backend Foundation)
- [ ] Multi-alarm CRUD API
- [x] MQTT service layer (redesigned) - **COMPLETED 2026-01-31**
  - Connection management with exponential backoff
  - State monitoring with observable pattern
  - Command publishing with brightness ramping
  - Interruption detection for manual intervention
  - 28 unit tests passing, integration tests prepared
  - See [IMPLEMENTATION-MQTT-SERVICE-LAYER.md](IMPLEMENTATION-MQTT-SERVICE-LAYER.md)
- [ ] Alarm state machine with single dimmer
- [ ] PostgreSQL persistence
- [ ] Interruption detection
- [ ] Docker multi-arch build
- [ ] API documentation (OpenAPI/Swagger)

## Phase 2: Feature Completeness
- [ ] Execution history & logging
- [ ] Advanced brightness curves (logarithmic, custom waypoints)
- [ ] Multiple dimmer devices support
- [ ] Alarm groups/presets
- [ ] Time zone per-alarm configuration
- [ ] Health check endpoints

## Phase 3: Frontend Integration
- [ ] Blazor Server UI (or alternative SPA)
- [ ] Real-time status updates (SignalR)
- [ ] Alarm editing UI
- [ ] Execution history viewer
- [ ] System settings/configuration UI

## Phase 4: Advanced Features
- [ ] Snooze functionality
- [ ] Gradual light scheduling (not just wake-up)
- [ ] Alarm rules & conditions
- [ ] Integration with external calendars
- [ ] Mobile app (PWA or native)

---
