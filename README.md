# Nuskha.ge

Social reading lists for Georgian books — think AniList, but for books. Readers build lists (reading / completed / plan to read…), rate books, follow each other, comment on profiles, like activity, and talk in a forum. The site is bilingual (Georgian default, English).

> **Status: first scaffold.** The code has been written but had **not been compiled** when it was committed (the authoring environment had no .NET SDK). Push it and let GitHub Actions build it; expect a few compile fixes on the first run.

## Stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10, ASP.NET Core Razor Pages |
| Interactivity | htmx (likes, comments, follow, list editor, infinite feed) |
| Styling | Tailwind CSS v4, mobile-first, light/dark via `prefers-color-scheme` |
| Data | PostgreSQL + EF Core (`pg_trgm` search indexes, ICU `ka-GE` collation for Georgian sorting) |
| Auth | ASP.NET Core Identity + Google sign-in, roles: Admin / Moderator / Editor / User |
| Hosting | Docker Compose (any Linux host, e.g. a Proxmox VM/LXC) behind a TLS reverse proxy |

Solution layout (a modular monolith):

```
src/Nuskha.Domain          entities and enums, no dependencies
src/Nuskha.Infrastructure  EF Core model, services (lists, social, feed, slugs), book importers
src/Nuskha.Web             Razor Pages UI, localization (Localization/ka.json, en.json), Tailwind
tests/Nuskha.Tests         unit tests (slugs, ISBN, username rules)
```

## URLs

Every public thing has a readable identifier in its URL:

| Page | URL |
|---|---|
| Book | `/books/{slug}` — slug is transliterated from the Georgian title (`ვეფხისტყაოსანი` → `vepkhistqaosani`) |
| Author | `/authors/{slug}` |
| Profile / list | `/user/{username}`, `/user/{username}/list` |
| Forum | `/forum`, `/forum/{category}`, `/forum/thread/{slug}` |
| Admin | `/admin`, `/admin/books`, `/admin/authors`, `/admin/users`, `/admin/reports`, `/admin/import` |

Usernames are chosen at registration (3–24 chars: `A–Z a–z 0–9 _ -`) and are part of the profile URL, so they cannot be changed yet.

## Roles

| Role | Can do |
|---|---|
| **User** | everyone: lists, comments, likes, follows, forum, submit books for review |
| **Editor** | edit/import catalog data, publish/hide books, edit authors |
| **Moderator** | publish/hide books, handle reports, lock/unlock ordinary accounts, pin/lock forum threads |
| **Admin** | everything, plus assigning roles and deleting books |

The first admin is created at startup from `Seed__AdminEmail` / `Seed__AdminPassword`. The admin panel refuses to remove the last admin.

## Run locally

Prerequisites: .NET 10 SDK, Node 22, Docker (for PostgreSQL).

```bash
docker run --name nuskha-db -e POSTGRES_USER=nuskha -e POSTGRES_PASSWORD=nuskha -e POSTGRES_DB=nuskha \
  -p 5432:5432 -d postgres:17

cd src/Nuskha.Web
npm install
npm run build          # Tailwind CSS + vendored htmx (use `npm run css:watch` while styling)
dotnet run
```

In Development the app creates the schema itself if there are no migrations and seeds an admin
(`admin@nuskha.local` / `ChangeMe-Dev-123` from `appsettings.Development.json` — **development only**).

## Migrations

Production start-up refuses to run without EF migrations. Generate the first one either:

* from GitHub: Actions → **Generate initial EF migration** → Run workflow (it commits the result), or
* locally: `dotnet tool install --global dotnet-ef`, then
  `dotnet ef migrations add InitialCreate -p src/Nuskha.Infrastructure -o Data/Migrations`

Later model changes: `dotnet ef migrations add <Name> -p src/Nuskha.Infrastructure -o Data/Migrations`. Pending migrations are applied automatically on start-up.

## Google sign-in

1. Google Cloud Console → APIs & Services → Credentials → *OAuth client ID* (Web application).
2. Authorized redirect URI: `https://<your-domain>/signin-google` (and `https://localhost:<port>/signin-google` for dev).
3. Set `Authentication__Google__ClientId` and `Authentication__Google__ClientSecret`. The button appears automatically once both are set.

A Google sign-in creates an account (username derived from the e-mail, e-mail marked confirmed). If a **password** account with the same e-mail already exists and its e-mail is not confirmed, Google sign-in is refused on purpose — otherwise someone could pre-register another person's address and keep access after the real owner signs in with Google.

## E-mail

Set `Email__Host`, `Email__From` (and usually `Email__UserName` / `Email__Password`; see `.env.example`) to any SMTP server — a transactional provider such as Brevo, Mailgun or Amazon SES, or your own relay. Once configured:

* new password accounts must confirm their address before they can sign in (Google accounts count as confirmed);
* `/account/forgot-password` and `/account/resend-confirmation` work; links are valid for 24 hours;
* links in mails are built from `Site__BaseUrl`, not the request's Host header — keep it correct.

Without SMTP the site behaves as before: registration signs in immediately and password reset is unavailable. **Turning e-mail on later** makes existing unconfirmed password accounts confirm their address on next sign-in (they can use "Resend confirmation"). Set up SPF/DKIM for your sending domain or mails will land in spam.

## Deploy on Proxmox (Docker Compose)

1. Create a Debian/Ubuntu VM or a privileged LXC with nesting enabled, install Docker.
2. `git clone` this repo, `cp .env.example .env`, edit the values (long random `DB_PASSWORD`, your admin e-mail/password).
3. `docker compose up -d --build` — the app listens on port 8080.
4. Put a TLS reverse proxy in front: Nginx Proxy Manager, Caddy, Traefik, or a **Cloudflare Tunnel** (no open ports needed) pointing at `http://<vm>:8080`. The compose file sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, so the app trusts `X-Forwarded-*` from that proxy — do not expose port 8080 directly to the internet.
5. Back up the `dbdata` (PostgreSQL) and `appdata` (uploads and cookie keys) volumes, e.g. with `pg_dump` plus Proxmox VM snapshots.

## Importing books

`/admin/import` queues a background import (one at a time) and shows run history.

| Source | State |
|---|---|
| Open Library | enabled — public API, Georgian filter (`language:geo`) |
| Google Books | enabled; optional API key in `Import__GoogleBooksApiKey` |
| biblusi.ge, sulakauri.ge | configured in `appsettings.json` but **disabled** |

The two Georgian shops are wired as generic sitemap + JSON-LD sources (`SitemapJsonLdSource`) and honour `robots.txt`, but they are off until someone has read each site's terms of use. Their coverage and markup were **not** verified (they were unreachable from the authoring environment). Enable one with `Import__Sites__0__Enabled=true`. Imports store only factual metadata (title, authors, ISBN, publisher, year, pages) plus a link back to the source — never descriptions or hot-linked covers. Imported books are published immediately; use `/admin/books` to hide any that look wrong. Readers can also submit missing books at `/books/submit`, which land in a review queue.

## CI

`.github/workflows/ci.yml` builds CSS, builds the solution and runs the tests on every push and pull request.

## Known gaps

* Changing the e-mail address of an account is not possible yet.
* No post/comment editing (delete only), no private messages, no genre/tag taxonomy yet.
* Georgian fonts are loaded from Google Fonts; self-host them if that is a privacy concern.
* Rate limiting covers sign-in/registration and the social endpoints (likes, comments, follows), plus book submission, new threads, settings and reports; saving a list entry and replying in a forum thread are not yet rate limited.
* The CSP in `Program.cs` assumes no inline scripts or styles; keep it that way when adding UI.
