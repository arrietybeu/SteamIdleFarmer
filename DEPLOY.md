# Deploy (VPS)

CI builds the image on every push to `main` and publishes it to GitHub Container
Registry: **`ghcr.io/arrietybeu/steamidlefarmer:latest`**. The VPS just pulls and runs it.

## One-time setup on GitHub
After the first successful **Build and publish container** run (Actions tab), make the
package public so the VPS can pull without logging in:
GitHub → your profile → **Packages** → `steamidlefarmer` → **Package settings** →
**Change visibility → Public**.
(If you'd rather keep it private, on the VPS run
`echo <YOUR_GHCR_PAT> | docker login ghcr.io -u arrietybeu --password-stdin` — a PAT with
`read:packages` scope.)

## On the VPS (Docker required)
```bash
mkdir -p ~/farmer && cd ~/farmer

# 1. Get the prod compose file
curl -O https://raw.githubusercontent.com/arrietybeu/SteamIdleFarmer/main/docker-compose.prod.yml

# 2. Create .env with a strong secret
printf 'FARMER_SECRET=%s\n' "$(openssl rand -base64 48)" > .env

# 3. Start
docker compose -f docker-compose.prod.yml up -d
```
Open `http://<vps-ip>:5080`.

## Update to the latest build
```bash
cd ~/farmer
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

## Domain & HTTPS
See the reverse-proxy section (added once a domain option is chosen). **GitHub Pages cannot
host this** — it only serves static files, not a live backend.
