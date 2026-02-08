# MQTT Reference

This document describes the MQTT topic configuration and message formats for communicating with Tasmota/Sonoff dimmer devices. These formats are derived from the Helios proof-of-concept and must be maintained for compatibility.

---

## Topic Configuration

MQTT topics are configurable via `appsettings.json`:

| Setting | Purpose | Example |
|---------|---------|---------|
| `Mqtt:Topics:DimmerOnOffCommand` | Turn dimmer on/off | `cmnd/dimmer/power` |
| `Mqtt:Topics:DimmerOnOffStatus` | Dimmer power status | `stat/dimmer/POWER` |
| `Mqtt:Topics:DimmerPercentageCommand` | Set brightness level | `cmnd/dimmer/dimmer` |
| `Mqtt:Topics:DimmerPercentageStatus` | Current brightness level | `stat/dimmer/RESULT` |

---

## Message Formats

All MQTT messages use UTF-8 encoded text. The dimmer device (typically Tasmota/Sonoff firmware) responds to commands and publishes status updates.

### Commands (Published by Application)

#### Turn On Command

**Topic:** `cmnd/dimmer/power`
**Payload:**
```json
{"POWER":"ON"}
```

#### Turn Off Command

**Topic:** `cmnd/dimmer/power`
**Payload:**
```json
{"POWER":"OFF"}
```

#### Set Brightness Command

**Topic:** `cmnd/dimmer/dimmer`
**Payload:**
```
50
```
*(Simple integer 0-100, no JSON)*

### Status Messages (Subscribed by Application)

#### Power Status

**Topic:** `stat/dimmer/POWER`
**Payload:**
```
ON
```
or
```
OFF
```

#### Brightness Status

**Topic:** `stat/dimmer/RESULT`
**Payload:**
```json
{"POWER":"ON","Dimmer":75}
```

---

## Minimum Brightness Threshold

The minimum brightness setting (default: 20%) is critical for hardware stability. **Lights operated by the dimmer begin to flicker when brightness is set below this threshold.** This is a physical hardware characteristic, not a software limitation.

**Behavior:**
- Wake-up sequences start at minimum percentage (not 0%) to avoid flickering
- Any dimmer command below the minimum is clamped to 0 (turn off) or the minimum percentage (turn on)
- Example: `SetPercentage(15)` with minimum 20% → publishes `20`

---

## Debug Commands

Monitor MQTT messages during development:

```bash
# Subscribe to all dimmer status topics
mosquitto_sub -h <mqtt-server> -u <username> -P <password> -t "stat/dimmer/#"

# Subscribe to all dimmer command topics
mosquitto_sub -h <mqtt-server> -u <username> -P <password> -t "cmnd/dimmer/#"

# Publish test commands
mosquitto_pub -h <mqtt-server> -u <username> -P <password> -t "cmnd/dimmer/power" -m '{"POWER":"ON"}'
mosquitto_pub -h <mqtt-server> -u <username> -P <password> -t "cmnd/dimmer/dimmer" -m '50'
```

---

## Document Metadata

**Source:** Helios proof-of-concept implementation
**Last Updated:** 2026-01-31
