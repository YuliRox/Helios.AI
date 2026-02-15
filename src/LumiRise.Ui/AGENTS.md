# AGENTS.md

This file is the startup contract for coding agents working in `src/LumiRise.Ui`.

## Goal

Load only the frontend context needed for the task. Do not scan the full repo first.

## Read These Files First

Read in order and stop once task scope is clear.

1. `README.md` - UI behavior and runtime overview.
2. `package.json` - scripts and dependency versions.
3. `angular.json` - build/serve setup and proxy config usage.
4. `src/app/app.component.ts` - main weekly calendar behavior.
5. `src/app/app.component.html` - dashboard/template structure.
6. `src/app/app.component.css` - component-level cyberpunk styling.
7. `src/styles.css` - global styles and font-face setup.
8. `src/app/services/alarm-api.service.ts` - API endpoints used by UI.
9. `src/app/models/api.models.ts` - request/response models.
10. `proxy.conf.json` - Angular CLI reverse proxy to backend.
11. `nginx.conf` - container reverse proxy behavior.

## Task-Specific Read Paths

For calendar interactions (drag, resize, create/edit/delete):

1. `src/app/app.component.ts`
2. `src/app/app.component.html`

For visual/theme changes:

1. `src/app/app.component.css`
2. `src/styles.css`
3. `../../samples/cyberpunk-layout.md`

For API wiring/contract alignment:

1. `src/app/services/alarm-api.service.ts`
2. `src/app/models/api.models.ts`
3. `../../samples/swagger.json`

For local/runtime delivery:

1. `proxy.conf.json`
2. `nginx.conf`
3. `Dockerfile`
4. `../../docker-compose.yml` (service `lumi-rise-ui`)

## Commands

Run from `src/LumiRise.Ui` unless noted.

- Install deps: `npm install`
- Start dev server (with proxy): `npm start`
- Build UI: `npm run build`
- Run tests: `npm test`
- Build/run UI container from repo root: `docker compose up -d --build lumi-rise-ui`

## Notes

- The UI is a single-page Angular dashboard focused on weekly alarm scheduling.
- API base URL defaults to same-origin and is proxied by Angular dev server / nginx config.
- Keep edits focused; avoid broad refactors when only style/interaction behavior is requested.
