# Installing Nuskha

Two paths: **A) run it on your own computer** to try or develop it, and **B) deploy it on a server** (your Proxmox) so people can use it. Do A first; it takes about 15 minutes.

---

## A. Run locally

### A1. Install the prerequisites

| Tool | Why | Windows | macOS | Linux |
|---|---|---|---|---|
| **Git** | get the code | `winget install Git.Git` | `brew install git` | `sudo apt install git` |
| **.NET 10 SDK** | build and run the app | `winget install Microsoft.DotNet.SDK.10` | `brew install --cask dotnet-sdk` | see [Microsoft's install page](https://learn.microsoft.com/dotnet/core/install/linux) |
| **Node.js 22 or newer** | builds the Tailwind CSS | `winget install OpenJS.NodeJS.LTS` | `brew install node` | [nodejs.org](https://nodejs.org/) or your package manager |
| **Docker** | runs PostgreSQL | [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Docker Desktop | `curl -fsSL https://get.docker.com \| sh` |

Open a **new** terminal afterwards and check:

```bash
git --version
dotnet --version     # should print 10.x
node --version       # 22 or higher
docker --version
```

(`winget` is built into Windows 10/11. On macOS, install [Homebrew](https://brew.sh) first if you don't have `brew`.)

### A2. Get the code

```bash
git clone https://github.com/HaowieD/nuskha.git
cd nuskha
```

### A3. Start the database

```bash
docker run --name nuskha-db -e POSTGRES_USER=nuskha -e POSTGRES_PASSWORD=nuskha -e POSTGRES_DB=nuskha \
  -p 5432:5432 -d postgres:17
```

Next time, just `docker start nuskha-db`. This matches the default connection string in `appsettings.json`, so no further configuration is needed.

### A4. Build the CSS and start the site

```bash
cd src/Nuskha.Web
npm install
npm run build
dotnet run
```

The console prints the address (normally `http://localhost:5000`). On first start the app creates the tables and an admin account:

* e-mail `admin@nuskha.local`, password `ChangeMe-Dev-123` (development only).

While styling, run `npm run css:watch` in a second terminal.

### A5. Optional: Google sign-in locally

1. Go to [Google Cloud Console](https://console.cloud.google.com/) → create a project → **APIs & Services → OAuth consent screen** (External, fill in the app name and your e-mail).
2. **Credentials → Create credentials → OAuth client ID → Web application**.
3. Authorized redirect URI: `http://localhost:5000/signin-google` (use the port from step A4).
4. Store the keys outside git:
   ```bash
   cd src/Nuskha.Web
   dotnet user-secrets set "Authentication:Google:ClientId" "<client id>"
   dotnet user-secrets set "Authentication:Google:ClientSecret" "<client secret>"
   ```
5. Restart `dotnet run`. The **Continue with Google** button appears.

While the consent screen is in "Testing" mode only the test users you list can sign in; publish it when you go live.

### A6. Optional: e-mail locally

Without SMTP nothing is sent and registration signs people in immediately, which is fine for development. To try the confirmation and reset mails you need a real SMTP server that supports STARTTLS (port 587) or TLS (port 465, also set `Email:ImplicitTls` to `true`), such as Brevo, Mailgun, Amazon SES or your own mail server. Local catch-all tools like Mailpit use unencrypted SMTP and will not work.

```bash
dotnet user-secrets set "Email:Host" "smtp.example.com"
dotnet user-secrets set "Email:Port" "587"
dotnet user-secrets set "Email:UserName" "<user>"
dotnet user-secrets set "Email:Password" "<password>"
dotnet user-secrets set "Email:From" "no-reply@yourdomain.ge"
```

Also set `Site:BaseUrl` to your local address (`dotnet user-secrets set "Site:BaseUrl" "http://localhost:5000"`), because mailed links are built from it.

### A7. Run the tests

```bash
cd nuskha        # repository root
dotnet test
```

---

## B. Deploy on Proxmox

Plan: one Debian/Ubuntu **VM** running Docker Compose (the app + PostgreSQL), and a way to reach it over HTTPS. A VM is simpler and safer than an LXC container for Docker.

### B1. Create the VM

1. In Proxmox, create a VM from a Debian 12/13 or Ubuntu 24.04 server ISO: 2 vCPU, 2–4 GB RAM, 20+ GB disk. Install with SSH server enabled.
2. Give it a fixed IP (DHCP reservation on your router, or static).
3. Log in over SSH, then install Docker and Git:
   ```bash
   sudo apt update && sudo apt install -y git
   curl -fsSL https://get.docker.com | sudo sh
   sudo usermod -aG docker $USER      # log out and back in afterwards
   docker compose version             # should print a version
   ```

### B2. Configure and start

```bash
git clone https://github.com/HaowieD/nuskha.git
cd nuskha
cp .env.example .env
nano .env
```

Fill in `.env`:

| Variable | Set to |
|---|---|
| `DB_PASSWORD` | a long random string (`openssl rand -base64 32`) |
| `SITE_URL` | your public address, e.g. `https://nuskha.ge` (no trailing slash) |
| `ADMIN_USERNAME`, `ADMIN_EMAIL`, `ADMIN_PASSWORD` | your first administrator, created on first start |
| `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` | optional, see A5 but with redirect URI `https://<your-domain>/signin-google` |
| `SMTP_*` | optional, see A6 |

Then:

```bash
docker compose up -d --build
docker compose logs -f web        # Ctrl+C to leave; look for "Now listening on"
```

The first build takes a few minutes. The site now answers on `http://<vm-ip>:8080`. The database migration is applied automatically on start. Sign in with the admin e-mail and password from `.env`, then open **/admin**.

### B3. Put HTTPS in front of it

Never expose port 8080 directly to the internet; the app trusts `X-Forwarded-*` headers from whatever is in front of it. Pick one:

**Option 1: Cloudflare Tunnel (no open ports, works behind home NAT).** Your domain must use Cloudflare DNS.
1. Cloudflare dashboard → **Zero Trust → Networks → Tunnels → Create a tunnel** (Cloudflared).
2. Copy the install command it shows and run it on the VM.
3. Add a **public hostname**: `nuskha.ge` → service `HTTP` → `localhost:8080`.
4. Done: Cloudflare provides the certificate.

**Option 2: Caddy on the same VM (automatic Let's Encrypt).** Needs ports 80/443 forwarded from your router to the VM and a DNS `A` record pointing at your public IP.
```bash
sudo apt install -y caddy
printf 'nuskha.ge {\n    reverse_proxy localhost:8080\n}\n' | sudo tee /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

**Option 3: Nginx Proxy Manager** or Traefik if you already run one: create a proxy host for your domain pointing at `http://<vm-ip>:8080` and request a Let's Encrypt certificate there.

If the proxy runs on the same VM, change the port line in `docker-compose.yml` to `"127.0.0.1:8080:8080"` so only the proxy can reach the app, then `docker compose up -d`.

### B4. Update to a new version

```bash
cd nuskha
git pull
docker compose up -d --build
```

Database migrations are applied on start.

### B5. Back up (do this before you have real users)

```bash
# database dump
docker compose exec -T db pg_dump -U nuskha nuskha | gzip > nuskha-$(date +%F).sql.gz

# restore into an empty database
gunzip -c nuskha-2026-10-26.sql.gz | docker compose exec -T db psql -U nuskha nuskha
```

Uploads (covers/avatars) and the cookie-signing keys live in the `appdata` volume. Proxmox VM snapshots or backups of the whole VM cover everything; schedule them under **Datacenter → Backup**. Copy backups off the machine.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `dotnet: command not found` | open a new terminal; check `dotnet --version` |
| Connection refused to `localhost:5432` | `docker ps` should show `nuskha-db`; start it with `docker start nuskha-db` |
| Page has no styling | run `npm run build` in `src/Nuskha.Web` |
| No **Continue with Google** button | both Google variables must be set; restart the app |
| Google says `redirect_uri_mismatch` | the redirect URI in Google Cloud must be exactly `<your address>/signin-google` |
| Links in e-mails point to the wrong address | fix `SITE_URL` / `Site:BaseUrl` |
| Can't sign in after enabling e-mail | existing unconfirmed accounts must confirm first: Login → "Resend confirmation" |
| Login redirects loop behind a proxy | keep `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (already in `docker-compose.yml`) and make sure the proxy sends `X-Forwarded-Proto` |
| `docker compose` can't find `.env` values | run it from the repository folder that contains `.env` |
