# Kickbase Assistant

Persönliches Tool-Set rund um Kickbase: Marktwert-Tracker, Aufstellungs-Wächter (LigaInsider),
Verkaufs-Alarm, Live-Punkte, Transfermarkt-Scanner und Liga-Analyse. Nutzt die inoffizielle,
community-dokumentierte Kickbase API v4 ([kevinskyba/kickbase-api-doc](https://github.com/kevinskyba/kickbase-api-doc)).

**Nur für den privaten Gebrauch.** Die Kickbase-API ist nicht offiziell, kann sich jederzeit
ändern, und zu viele/zu häufige Requests können den Account gefährden. Automatisches Bieten
oder Aufstellen ist bewusst nicht implementiert. LigaInsider-Daten (sobald Phase 4 dazu kommt)
dürfen nicht öffentlich angezeigt werden — die Seite gehört hinter einen Login.

## Architektur

```
backend/
  src/
    KickbaseAssistant.Domain          Entities (Player, MarketValueSnapshot, LineupPrediction, SellAlert, League)
    KickbaseAssistant.Infrastructure  EF Core (Postgres) + Kickbase-HTTP-Client (Login, Token-Refresh)
    KickbaseAssistant.Api             REST-API fürs Frontend, liest nur aus der DB, spricht nie direkt mit Kickbase
    KickbaseAssistant.Worker          Quartz.NET-Jobs, einziger Dienst mit Kickbase-Zugangsdaten
docker-compose.yml                    Postgres + Api + Worker
```

Das Frontend (später Angular) spricht ausschließlich mit der Api. Kickbase-Login und Token
bleiben serverseitig im Worker.

## Setup (lokal, ohne Docker)

1. Postgres lokal bereitstellen (oder `docker compose up postgres`).
2. Kickbase-Zugangsdaten per User Secrets setzen (niemals in appsettings.json committen):
   ```
   cd backend/src/KickbaseAssistant.Worker
   dotnet user-secrets set "Kickbase:Email" "deine-email@example.com"
   dotnet user-secrets set "Kickbase:Password" "dein-passwort"
   dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=kickbase_assistant;Username=kickbase;Password=..."
   ```
   Für die Api reicht der ConnectionString (kein Kickbase-Login nötig):
   ```
   cd backend/src/KickbaseAssistant.Api
   dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=kickbase_assistant;Username=kickbase;Password=..."
   ```
3. Datenbank migrieren:
   ```
   cd backend
   dotnet ef database update --project src/KickbaseAssistant.Infrastructure --startup-project src/KickbaseAssistant.Api
   ```
4. Starten:
   ```
   dotnet run --project src/KickbaseAssistant.Api      # Swagger unter /swagger
   dotnet run --project src/KickbaseAssistant.Worker    # läuft den KickbaseHealthCheckJob alle 15 Min
   ```

## Setup (Docker Compose)

1. `.env` aus `.env.example` anlegen und ausfüllen (DB-Passwort, Kickbase-Login).
2. `docker compose up --build`.
3. Api erreichbar unter `http://localhost:5080`.

## Stand / offene Punkte

Dies ist das **Grundgerüst (Phase 1)**: Solution-Struktur, DB-Schema, Kickbase-Login mit
Token-Refresh, ein Healthcheck-Job als Vorlage, Api mit Health-/Players-Endpoint und
SignalR-Hub-Platzhalter für Live-Punkte.

Die genauen JSON-Feldnamen der Kickbase-Antworten sind in der Community-Spec nur grob
paraphrasiert (kein `components.schemas`-Abschnitt vorhanden) und daher **nicht verifiziert**.
`KickbaseApiClient` gibt deshalb aktuell rohes `JsonElement` zurück. Bevor Phase 2
(Marktwert-Tracker) startet: einmal mit echten Zugangsdaten gegen den `KickbaseHealthCheckJob`
laufen lassen, die echten Feldnamen aus den Logs/einer echten Antwort ablesen und daraus
typisierte DTOs bauen.

Geplante Phasen (siehe Projektverlauf):
2. Marktwert-Tracker · 3. Deployment · 4. LigaInsider + Aufstellungs-Wächter ·
5. Verkaufs-Alarm · 6. Live-Punkte (SignalR) · 7. Transfermarkt-Scanner ·
8. Liga-Analyse (inkl. Gebotsverhalten-Analyse) · 9. Punkteprognose + Aufstellungs-Optimierer
