# Academic Clarity

**Scientific Journal Publication Trend Tracking System**
Project code **SU25SWP06** · Team **SE1918 – Team 2** · 2026

Academic Clarity is a full-stack web platform that turns raw scholarly metadata into
actionable insight for researchers. It synchronizes Computer Science papers from
**OpenAlex**, enriches them with AI-extracted keywords, and lets users explore publication
**trends**, browse a keyword **mind map**, run an AI **idea-overlap check** against the
corpus, and draft papers in a built-in **LaTeX writer** — all behind a Free / Premium model
with real payments via **PayOS**.

> **Live demo:** https://academicclarity.tuanvo13072005.top

---

## Table of contents

- [Key features](#key-features)
- [Tech stack](#tech-stack)
- [Repository layout](#repository-layout)
- [Getting started (local dev)](#getting-started-local-dev)
- [Configuration](#configuration)
- [Deployment (Docker)](#deployment-docker)
- [User roles & access tiers](#user-roles--access-tiers)
- [External integrations](#external-integrations)
- [Team](#team)

---

## Key features

**Data & AI engine**
- **Data Sync** — fetches Computer Science papers from OpenAlex page by page, with a
  quality gate (primary field = CS, has abstract, English, article/proceedings only) and
  deduplication by OpenAlex ID / ISSN / author name. Only one sync job runs at a time.
- **Data Cleaning** — runs inline during sync: keyword normalization, ~90 generic
  stop-keywords filtered, confidence score ≥ 0.45, max 8 keywords per paper.
- **AI Keyword Extraction** — a background job enriches new papers with keywords via a
  local Ollama model (hybrid: OpenAlex ∪ AI, deduped), roughly doubling index density
  (~3.6 → ~7.1 keywords/paper).

**Discovery & analytics**
- **Search** — by keyword / title / author / journal, ranked by citations, paginated, with
  saved search history and autocomplete.
- **Mind map** — a 3-level tree of co-occurring keywords derived from the data
  (co-occurrence ≥ 2), node size by paper count and color by recency.
- **Trend Analytics** — relative-frequency (share) time series per keyword, with a
  least-squares regression slope over a recent window to label topics rising / falling /
  stable, top-risers ranking, and CSV export (Premium).

**AI idea-overlap checker (Premium)**
- Paste an abstract → keywords extracted by Gemini (with Ollama fallback) → flexible
  concept-level matching → per-paper score = **IDF-weighted overlap × coverage** →
  verdict **High / Moderate / Low**. Designed as an *early warning*, not a plagiarism tool.

**Authoring, subscription & admin**
- **LaTeX Writer** — client-side editor with citation insertion and PDF preview (Premium).
- **Bookmarks, Follow & real-time notifications** — SignalR push, system-wide broadcast.
- **Subscription & Payment** — PayOS VietQR, Monthly / Yearly plans, 50% discount for
  `.edu` accounts, resilient PENDING-order reconciliation.
- **Admin console** — dashboard, data pipeline (Sync Now, SCImago CSV import), user &
  plan management, revenue, sync logs and activity audit trail.

---

## Tech stack

| Layer | Technology |
|---|---|
| **Frontend** | React 19 · TypeScript · Vite · Tailwind CSS · Recharts · vis-network · CodeMirror |
| **Backend** | .NET 8 Web API · Entity Framework Core · SignalR · Microsoft Semantic Kernel |
| **Database** | MySQL (Pomelo EF Core provider) — 21 tables |
| **Auth** | JWT (access + refresh) · BCrypt · email OTP (MailKit) |
| **AI** | Gemini (via OpenRouter / Semantic Kernel) · Ollama (local keyword extraction) |
| **Data sources** | OpenAlex API (papers) · SCImago (journal rankings) |
| **Payments** | PayOS (VietQR) |
| **Deployment** | Docker Compose · Caddy (reverse proxy + automatic HTTPS) |

---

## Repository layout

```
Academic Clarity/
├── backend/
│   ├── ScientificTrendTracker.slnx
│   └── ScientificTrendTracker/        # .NET 8 Web API (Controllers, Services, Models, Migrations)
├── frontend/                          # React + TypeScript + Vite SPA
│   └── src/                           # pages, components, lib/api, context, routes
├── deploy/                            # docker-compose.yml + MySQL seed (initdb)
└── docs/                              # design docs, demo scripts, presentation outline
```

---

## Getting started (local dev)

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- MySQL 8 (local or remote)
- *(optional)* [Ollama](https://ollama.com/) running `qwen2.5:7b-instruct` for AI keyword extraction

### 1. Backend

```bash
cd backend/ScientificTrendTracker
# create appsettings.json (see Configuration below), then:
dotnet restore
dotnet run
# API on http://localhost:5024  ·  Swagger at /swagger
```

The database schema is provided as a dump; import it into your MySQL instance, or point the
connection string at an existing Academic Clarity database.

### 2. Frontend

```bash
cd frontend
cp .env.example .env        # defaults proxy /api → http://localhost:5024
npm install
npm run dev                 # http://localhost:5173
```

Useful scripts: `npm run build` (type-check + production build) · `npm run lint` · `npm run preview`.

---

## Configuration

Secrets live in `backend/ScientificTrendTracker/appsettings.json` (git-ignored) and
`frontend/.env` (git-ignored). Create them locally.

**`appsettings.json` — required keys (replace placeholders):**

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your_host;Port=your_port;Database=your_database_name;Uid=your_account;Pwd=your_password;AllowUserVariables=True;TreatTinyAsBoolean=True"
  },
  "Jwt": { "SecretKey": "your_jwt_secret_min_32_bytes", "Issuer": "your_issuer", "Audience": "your_audience" },
  "OpenAlex": { "BaseUrl": "your_openalex_base_url", "Email": "your_email" },
  "PayOS": { "ClientId": "your_client_id", "ApiKey": "your_api_key", "ChecksumKey": "your_checksum_key", "ReturnUrl": "your_return_url", "CancelUrl": "your_cancel_url" },
  "OverlapAi": { "BaseUrl": "your_ai_base_url", "ApiKey": "your_ai_api_key", "Model": "your_model_name" },
  "EmailSettings": { "Server": "your_smtp_server", "Port": your_smtp_port, "SenderEmail": "your_sender_email", "Username": "your_username", "Password": "your_app_password" },
  "WeeklySync": { "Enabled": false }
}
```

**`frontend/.env`:**

```
VITE_API_BASE_URL=/api
VITE_API_PROXY_TARGET=http://localhost:5024
```

> ⚠️ Never commit real secrets. `appsettings.json` and `.env` are excluded by `.gitignore`.

---

## Deployment (Docker)

The `deploy/` folder ships a one-host stack: **MySQL** (data on a volume) + **backend**
(.NET 8) + **Caddy** (serves the built frontend, reverse-proxies `/api`, and provisions
automatic HTTPS via Let's Encrypt).

```bash
# place a MySQL dump at deploy/initdb/01-dump.sql to seed on first boot (optional)
cd deploy
docker compose up -d --build
```

Set the production domain and secrets via environment overrides in `deploy/docker-compose.yml`
(`Cors__AllowedOrigins__0`, `PayOS__ReturnUrl`, `ConnectionStrings__DefaultConnection`, …).
Point a DNS `A` record at the host and Caddy handles the certificate automatically.

---

## User roles & access tiers

Two independent axes — **role** (identity) and **access tier** (feature entitlement):

| RoleId | Role | Created when |
|---|---|---|
| 1 | Admin | Seeded |
| 2 | Researcher | Automatically on successful payment |
| 3 | Student (edu) | Registration with an `.edu` / `.edu.vn` email |
| 4 | Regular User | Registration with a normal email |

| Access tier | Meaning |
|---|---|
| BASIC | Free features |
| PREMIUM | AI overlap, advanced trends + export, full LaTeX writer |

Admins bypass all Premium gates. `.edu` accounts get a 50% discount on Premium plans.

---

## External integrations

- **OpenAlex** — source of papers (polite pool via `OpenAlex:Email`).
- **SCImago** — journal quartile / impact-factor / H-index import via CSV.
- **Gemini** (OpenRouter / Semantic Kernel) — idea-overlap keyword extraction & analysis.
- **Ollama** — local model for background keyword enrichment.
- **PayOS** — VietQR payments, verified via webhook and return-URL reconciliation.

---

## Team

| Student ID | Name | Role |
|---|---|---|
| SE193532 | Võ Thanh Tuấn | Leader |
| SE193659 | Mai Thành Được | BE |
| SE193549 | Đặng Thành Tài | BE |
| SE193160 | Ngô Đức Tài | FE |
| SE196278 | Hoàng Tiến Đạt | FE |

---

*Academic Clarity — from simple paper search to visual trend discovery and research decision support.*
