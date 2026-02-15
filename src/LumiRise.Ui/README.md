# LumiRise UI (Angular)

Dashboard frontend for the API in `samples/swagger.json`.

## Features

- Weekly calendar view (Monday to Sunday)
- 15-minute vertical timeline from `00:00` to `24:00`
- Alarm rendering by duration (based on `Ramp.rampDurationSeconds`)
- Cross-day alarm display when duration crosses midnight
- Click alarm to open `Modify` and `Delete` actions
- Create new alarms from dashboard
- Delete flow with confirmation prompt
- Drag alarms vertically and horizontally in 15-minute/day increments
- Horizontal expand/shrink handles to apply the same alarm across multiple days
- Collision prevention (no overlap with existing alarms)

## Run

```bash
cd src/LumiRise.Ui
npm install
npm start
```

The default API base URL is same-origin (`''`), so `/api/*` calls go to the current host.

For local Angular CLI development, requests to `/api`, `/swagger`, and `/hangfire`
are proxied to `http://localhost:8080` via `proxy.conf.json`.

For Docker Compose runtime, the Nginx-hosted UI is available at `http://localhost:8081`
and reverse-proxies API routes to the backend configured through `BACKEND_URL`
(in compose this is `http://lumi-rise:8080`).

You can override it before app bootstrap by setting:

```html
<script>
  window.__LUMIRISE_API_BASE_URL__ = 'http://localhost:8080';
</script>
```
