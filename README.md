# Steam Idle & Achievement Farmer ("Xưởng Cày")

![Xưởng Cày — the manual achievement unlock view](assets/Screenshot.png)

*The manual-unlock view. Your Steam library sits on the left with real playtime per game;
pick one and it loads every achievement that game has — icon, description, and current state.
Diablo IV here is finished at 45/45. Tick whichever ones you want and hit unlock. Achievements
the publisher locks server-side are detected and disabled up front, so a run never silently
stalls on something that can't be set. The whole interface runs in English or Vietnamese and
updates live over WebSocket — the other tab, "Idle to 100%", is where long unattended runs live.*

Self-hosted web tool to idle Steam playtime and unlock achievements on **your own**
account. Log in with a QR code (Steam Mobile) — no password. Each browser is its own
session, so friends can log in and farm their own accounts. Built to run 24/7 on a VPS.

**Two features**

1. **Idle to 100%** — pick games, set hours (default 200); it idles them (real playtime +
   "in-game" status) and drips the unlockable achievements evenly until the game hits 100%.
2. **Manual unlock** — pick a game, tick achievements, unlock instantly (SAM-style).

## ⚠️ Read first

- Violates Steam's ToS (fake playtime + spoofed achievements). The account-limit risk is
  real — use at your own risk, on accounts you own.
- **Never** use it on VAC / anti-cheat online games. Protected (server-side) achievements
  are detected and skipped automatically.
- Refresh tokens are encrypted at rest (AES-GCM, key from `FARMER_SECRET`). Many accounts
  farming from one VPS IP draw more attention.
- Playtime only accrues while the VPS is up and logged in — 200h means 200h of real idling.

## Quick start (Docker)

```bash
cp .env.example .env      # set a strong FARMER_SECRET, e.g. openssl rand -base64 48
docker compose up -d --build
```

Open `http://<vps-ip>:5080` → **Log in with Steam** → scan the QR → pick games → idle or
unlock. Data (the encrypted DB) lives in `./data`.

## Development

Requires **.NET SDK 10** and **Node 22**.

```bash
# backend (port 5080)
cd backend && FARMER_SECRET=dev dotnet run --project src/SteamFarmer.Api

# frontend (proxies /api and /ws to :5080)
cd frontend && npm install && npm run dev
```

Run the tests: `cd backend && dotnet test`.

## Configuration (env)

| Variable | Default | Purpose |
|---|---|---|
| `FARMER_SECRET` | — | Encrypts stored Steam refresh tokens. Set it in production. |
| `FARMER_ACCESS_PASSWORD` | off | Optional app-level access gate for a public instance. |
| `FARMER_DEVICE_NAME` | `SteamIdleFarmer` | Name shown in the Steam Mobile confirmation. |
| `PORT` | `5080` | HTTP port. |

## Stack & notes

.NET 10 (ASP.NET Core) on **SteamKit2** · **React + Vite** frontend · **SQLite**. The backend
serves the built frontend. When you expose it publicly, put it behind a reverse proxy with
TLS and authentication.

## License

[MIT](LICENSE) © 2026 arrietybeu

## Credits

- Steam networking via [SteamKit2](https://github.com/SteamRE/SteamKit).
- Achievement handling inspired by [gibbed's SteamAchievementManager](https://github.com/gibbed/SteamAchievementManager).
