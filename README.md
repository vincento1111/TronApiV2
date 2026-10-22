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
- `/api/game/train` consumes energy and changes one stat atomically. Its response is `{stats, stat, energySpent, statGain, xpGained, levelsGained}`; a batch advances mastery and rolls its small random variation separately for every energy actually spent.
- `/api/items` describes item types; `/api/inventory` holds individual weapon/training instances and consumable stacks. One weapon and one training item can be equipped independently. Each training item's precise bonus is rolled once when bought and survives renaming, equipping and restarting.
- `/api/chat/messages` persists messages and supports idempotent send retries and history cursors. `/hubs/chat` sends authenticated realtime notifications. Direct messages are restricted to their two participants.
- `/api/health` identifies the running local application for launch readiness.

All acting-player identities come from the verified session. Public JSON never contains account passwords, password hashes, or another player's email. SQLite immediate write transactions protect money, energy, quantities and equipment against concurrent requests. Energy uses elapsed time from persisted timestamps rather than a background polling job.

## Progression and precision

New players start with four stats at **1**, bounded at **100**. Stats are stored as signed 64-bit integers with `StatScale = 1,000,000,000`, then returned as decimal JSON numbers; small gains retain nine decimal places. An item's bonus uses integer parts per million, so `34572` is exactly **3.4572%**. See `TronApi/Services/ProgressionRules.cs` for the named constants and formulas.

Each energy trains the selected stat by `0.0125 / stat^0.75`, multiplied by accumulated mastery, equipped training gear, and an independent random factor from 0.97 to 1.03. Gains round to nine decimal places and stop at 100. Energy that would be spent after reaching that cap is retained. Training beyond the available energy is rejected; an already capped stat returns zero spend and zero XP.

Each energy actually spent earns 10 XP, independently of stat gains and equipment. The next-level cost at level L is `ceil(500 + 20*(L-1) + 0.0008*(L-1)^4)`. Level and XP cap at 100 and 1,629,982 respectively. Maximum health is `100 + 5*(level-1)`; levelling adds the increase to current health while retaining existing damage.

The stats response also includes total XP, XP earned in the current level, that level's full XP cost (`xpToNextLevel`, zero at level 100), total training energy, mastery and active equipment bonuses, and the remaining daily consumable allowance/reset time. Natural regeneration remains one energy every ten minutes, capped at 72.

All consumables share a **24 restored-energy allowance per UTC day**. Only energy actually restored counts. The final item may restore partially when the daily or energy cap is close; oversized quantities are rejected before items are consumed. Coffee, soda and trail mix cannot bypass the shared allowance.

## Validation

```sh
dotnet build TronApi.sln -c Release
dotnet test TronApi.sln -c Release
```

The frontend repository's `e2e` suite additionally exercises the real HTTP API and SignalR with isolated players and concurrent requests. Use a separate test database for that suite.

## Existing data and deployment

This local database remains separate from the old Azure database. No old database is contacted. Importing old Azure accounts, messages or inventories requires a backed-up, explicit migration; old plaintext passwords need a reset or controlled conversion. Credentials previously committed to Git remain in historical commits and should be rotated if still in use.

The progression update **upgrades existing local revival saves automatically to schema v2**. Stop the old server before running the new build. Before changing an existing save, the SQLite backup API writes a consistent snapshot to `App_Data/backups/<save-name>.pre-v2.<timestamp>.<id>.db` (or a `backups` folder beside a custom database path). The backup includes committed WAL content. Schema changes, stat conversion and the version marker commit together; a failed upgrade rolls back and retains the backup. Repeated startup does not convert stats again. Unknown newer schemas are refused.

Accounts, password hashes, sessions, chat, item IDs/names/rolls, stack quantities, money and remaining energy are preserved. Legacy stats started at 10 and gained one per energy, so migration recovers each stat's prior work as `max(0, oldStat-10)`. Untrained 10 becomes the new baseline 1. Earned work is mapped onto the expected new curve using an efficient analytic conversion, and gives the same total training energy and 10 XP per energy up to the XP cap. Because the old release stored no training order, conversion uses strength, defense, speed, then dexterity deterministically. Existing health damage is preserved when the resulting level raises maximum health.

To restore, stop every server using the save, retain the failed/new save separately, and restore the snapshot using the matching older application version. Keep the existing data-protection `keys` directory if preserving cookie sessions. Backups and saves are ignored by Git; never commit them.

The old Azure deployment workflow has been replaced by build/test checks. Public deployment is a separate step requiring HTTPS, production configuration, backups and disabling demo seeding. Combat, missions, crimes and player trading are not implemented in this milestone.
