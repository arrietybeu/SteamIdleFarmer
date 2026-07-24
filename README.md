# Steam Idle & Achievement Farmer ("Xưởng Cày")

![Xưởng Cày — the manual achievement unlock view](assets/Screenshot.png)

*The manual-unlock view. Your Steam library sits on the left with real playtime per game;
pick one and it loads every achievement that game has — icon, description, and current state.
Diablo IV here is finished at 45/45. Tick whichever ones you want and hit unlock. Achievements
the publisher locks server-side are detected and disabled up front, so a run never silently
stalls on something that can't be set. The whole interface runs in English or Vietnamese and
updates live over WebSocket — the other tab, "Idle to 100%", is where long unattended runs live.*

Self-hosted web tool to idle Steam playtime and unlock achievements on **your own** account.
QR login via Steam Mobile, no password. Each browser is its own session, so friends can farm
their own accounts. Built to run 24/7 on a VPS.

- **Idle to 100%** — pick games, set hours, it idles them and drips achievements until 100%.
- **Manual unlock** — pick a game, tick achievements, unlock instantly (SAM-style).

## ⚠️ Read first

- Breaks Steam's ToS — the account-limit risk is real. Use it on accounts you own.
- **Never** on VAC / anti-cheat games. Server-side protected achievements are auto-skipped.
- Playtime only accrues while the VPS is up and logged in.
- Tokens are encrypted at rest, but the server operator still holds them — only log in
  somewhere you trust.

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

## Stack

.NET 10 (ASP.NET Core) on **SteamKit2** · **React + Vite** · **SQLite**. The backend serves the
built frontend. Expose it publicly only behind a reverse proxy with TLS.

## License & credits

[MIT](LICENSE) © 2026 arrietybeu · Steam networking via
[SteamKit2](https://github.com/SteamRE/SteamKit) · achievement handling inspired by
[gibbed's SteamAchievementManager](https://github.com/gibbed/SteamAchievementManager).
