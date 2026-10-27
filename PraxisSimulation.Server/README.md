# ASP.NET-Core-WebSocket-Server – erster Live-Schritt

Start aus dem Repository-Hauptverzeichnis:

```powershell
dotnet run --project PraxisSimulation.Server --urls http://localhost:5080
```

Browser: http://localhost:5080. Zwei Tabs besitzen getrennte Sitzungen.
Die HTML-Patientenanzeige ist ein Verbindungstest. Die Unity-Live-Testszene
verwendet denselben WebSocket-Endpunkt.

## Unity-Projekt

Die erste Unity-Anbindung liegt unter `C:\Users\marle\arztdemo`.
Menue: **Tools > Arztpraxis > SimSharp Live-Testszene erstellen**; danach Play
und **Simulation starten**. Die bisherige SampleScene bleibt erhalten.

Fuer den Browser **Tools > Arztpraxis > SimSharp Web-Test bauen** ausfuehren,
dann im SimSharp-Repository:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Copy-UnityWebBuild.ps1
```

Bei laufendem Server ist der Webbuild unter http://localhost:5080/unity/ erreichbar.
Build-Dateien werden lokal kopiert und nicht in Git aufgenommen.

## Protokoll v1

WebSocket: `ws://localhost:5080/ws/simulation`

Client sendet:

```json
{"type":"start","speed":60}
```

`speed`: Simulationssekunden pro Echtzeitsekunde, 1 bis 600, Standard 60.
Stoppen: `{"type":"stop"}`. Anschließend kann ein neuer Lauf gestartet werden.

Server sendet `ready`, `started`, `event`, `completed`, `stopped` oder `error`.
Ereignisse enthalten `sessionId`, `runId`, fortlaufende `sequence`, `dayIndex: 0`
und `data` mit `zeit` (Minuten), `eventTyp`, `patientId`, `vonZustand`,
`zuZustand`, `arztId`, `schwesterId`. JSON-Eigenschaften sind camelCase.
Die Empfangsreihenfolge und Sequenznummer sind bei gleichen Zeitstempeln maßgeblich.

## Architektur und Grenzen dieses Schritts

- Core verwendet verlinkte Originalquellen, ohne WPF, ScottPlot oder Windows-Zielplattform.
  Das Desktop-Projekt bleibt vorerst bei seiner bisherigen Quelleneinbindung.
- Der bestehende Patientenprozess bereitet einen Tag vor. Der Runner verarbeitet
  SimSharp-Ereignisse erst bei Erreichen ihres Echtzeit-Zielpunkts (`Peek`/`Step`).
  Eine monotone Stopwatch ist die Zeitbasis; kein vorab berechnetes Playback.
- Je Verbindung eigener Datencontainer, Zufallsgenerator, Ressourcen und Lauf.
- Die globale Konfiguration wird im separaten Serverprozess einmalig geladen und danach
  ausschließlich gelesen. Individuelle Konfiguration ist noch nicht unterstützt;
  vor deren Einführung müssen alle Konfigurationszugriffe sitzungsbezogen werden.
- Ein einzelner Sender liest den begrenzten Channel (2048 Nachrichten).
  Die Simulation wartet nicht auf das Netzwerk. Ein voller Puffer beendet den Lauf;
  Ereignisse werden nicht stillschweigend verworfen und danach weitergerechnet.
- Höchstens 16 Verbindungen; Ende nach spätestens 15 Minuten; Keepalive und Sendetimeout.
  Verbindungsende bricht den zugehörigen Lauf ab. Keine Wiederaufnahme nach Disconnect.
- Start, Stop, Neustart nach Stop; noch keine Pause, Uhrnachrichten, Snapshots,
  individuellen Parameter oder Übertragung der vollständigen Auswertung.
- Die bestehende `SimulationEventQueue` bleibt für Desktop unverändert. Der Server
  nutzt einen begrenzten Channel für Ereignisse **und** Protokollnachrichten.
- Dies ist ein lokaler Entwicklungsserver. Vor öffentlichem Hosting: HTTPS/WSS,
  Nutzer-/Sitzungszugriff, Limits nach Nutzer, Proxy-Konfiguration und abgestimmte
  Origin-Freigabe. Aktuell sind Browser nur vom selben Origin zugelassen.

Prüfen:

```powershell
dotnet build PraxisSimulation.Server/PraxisSimulation.Server.csproj
dotnet build simSharpSimulation/simSharpSimulation.csproj
powershell -ExecutionPolicy Bypass -File scripts/Test-WebSocket.ps1
```

Das Testskript setzt einen auf Port 5080 laufenden Server voraus.
