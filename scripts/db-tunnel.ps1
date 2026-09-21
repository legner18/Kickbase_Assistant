# Baut einen SSH-Tunnel zur PostgreSQL-Datenbank auf dem Strato-Server auf.
# Postgres ist dort nur an 127.0.0.1 gebunden (kein offener Port im Internet).
# Waehrend dieses Skript laeuft, ist die DB lokal unter localhost:15434 erreichbar.
# Eigener lokaler Port (15434), damit der Tunnel parallel zum Klassenkasse-Tunnel (15432)
# und zum Habit-Tracker-Tunnel (15433) laufen kann.
# Terminal offen lassen, solange lokal entwickelt wird (Strg+C zum Beenden).

ssh -N -L 15434:127.0.0.1:5432 root@87.106.86.45
