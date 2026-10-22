# Tron City API

ASP.NET Core 10 backend for the [Tron City Angular client](https://github.com/vincento1111/TronHjemmeside). The revival branch replaces the prototype's browser-supplied identity and SQL Server dependency with verified sessions, a fresh local SQLite database, public response models, transactional inventory/game actions, and reliable global/direct chat.

## Run the playable local version

Check out `codex/revive-tron-city` in both repositories and place them beside each other. Run `Start-Tron.cmd` in `TronHjemmeside`. It builds both applications and starts **http://localhost:5077**. See the frontend README for demo accounts, the launcher, and the explanation of the changes.

To run only the API, use a .NET 10 SDK:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5077'
$env:Tron__SeedDemo = 'true'
dotnet run --project TronApi --no-launch-profile
```

Configuration uses environment variables or `TronApi/appsettings.json`:

| Setting | Meaning |
| --- | --- |
| `Tron__DatabasePath` | SQLite path, absolute or relative to the API content root; default `App_Data/tron-city.db` |
| `Tron__SeedDemo` | Opt-in demo players; only honoured in Development |
| `ASPNETCORE_URLS` | Local bind URL; launcher uses `http://localhost:5077` |

The demo players are `vince@tron.local` and `nova@tron.local`, password `TronLocal!2026`. Each initially owns 1,000 sodas and two separately tracked swords. Use independent browser profiles for separate accounts. Data persists between restarts.

## API design

- `/api/csrf` supplies the antiforgery token required as `X-CSRF-TOKEN` on modifying requests.
- `/api/auth` manages password-hashed accounts and HTTP-only cookie sessions.
- `/api/players/{id}` exposes a public profile. `/api/players/me` edits the signed-in player's profile.
- `/api/game/train` consumes energy and changes one stat atomically.
- `/api/items` describes item types; `/api/inventory` holds individual weapon instances and consumable stacks.
- `/api/chat/messages` persists messages and supports idempotent send retries and history cursors. `/hubs/chat` sends authenticated realtime notifications. Direct messages are restricted to their two participants.
- `/api/health` identifies the running local application for launch readiness.

All acting-player identities come from the verified session. Public JSON never contains account passwords, password hashes, or another player's email. SQLite immediate write transactions protect money, energy, quantities and equipment against concurrent requests. Energy uses elapsed time from persisted timestamps rather than a background polling job.

## Validation

```sh
dotnet build TronApi.sln -c Release
dotnet test TronApi.sln -c Release
```

The frontend repository's `e2e` suite additionally exercises the real HTTP API and SignalR with isolated players and concurrent requests. Use a separate test database for that suite.

## Existing data and deployment

This is a **new local schema**, not an automatic migration of the old Azure database. No old database is contacted. Importing old accounts, messages or inventories requires a backed-up, explicit migration; old plaintext passwords need a reset or controlled conversion. Credentials previously committed to Git remain in historical commits and should be rotated if still in use.

The old Azure deployment workflow has been replaced by build/test checks. Public deployment is a separate step requiring HTTPS, production configuration, backups and disabling demo seeding. Combat, missions, crimes and player trading are not implemented in this milestone.
