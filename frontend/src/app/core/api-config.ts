// Lokale Entwicklung (`ng serve`, <base href="/">): Api läuft separat per `dotnet run`
// (Profil "http", Port 5181 laut launchSettings.json).
//
// Produktion: Die App wird mit `--base-href /<versteckter-pfad>/` gebaut und unter genau diesem
// Pfad auf dem Server ausgeliefert. Api und SignalR-Hub laufen dort per Apache-Reverse-Proxy
// unter demselben Pfad (z. B. https://lukas-egner.de/<pfad>/api/…) - alles same-origin, kein CORS
// nötig. Der Pfad wird zur Laufzeit aus dem <base>-Tag gelesen statt fest einkompiliert, damit
// lokal und produktiv derselbe Build-Prozess funktioniert.
function resolveApiBaseUrl(): string {
  const baseHref = document.querySelector('base')?.getAttribute('href') ?? '/';
  if (baseHref === '/') {
    return 'http://localhost:5181';
  }
  return `${window.location.origin}${baseHref}`.replace(/\/$/, '');
}

export const API_BASE_URL = resolveApiBaseUrl();
