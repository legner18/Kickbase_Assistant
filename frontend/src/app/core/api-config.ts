// Lokale Entwicklung: Api läuft separat per `dotnet run` (Profil "http", Port 5181 laut
// launchSettings.json). Für ein echtes Production-Build muss das durch eine relative URL
// ersetzt werden, sobald Apache /api/ auf die Api reverse-proxied (siehe Backend-DEPLOYMENT-Notizen).
export const API_BASE_URL = 'http://localhost:5181';
