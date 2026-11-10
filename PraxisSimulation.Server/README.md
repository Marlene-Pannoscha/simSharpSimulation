# ASP.NET-Core-WebSocket-Server – erster Live-Schritt

Start aus dem Repository-Hauptverzeichnis:

```powershell
dotnet run --project PraxisSimulation.Server --urls http://localhost:5080
```

Browser: http://localhost:5080. Zwei Tabs besitzen getrennte Sitzungen.
Die HTML-Patientenanzeige ist ein Verbindungstest. Die Unity-Live-Testszene
verwendet denselben WebSocket-Endpunkt.

## Unity-Projekt

Die vorhandene Raumaufteilung ist in `Assets/Scenes/PraxisLive.unity` integriert.
In Unity **Tools > Arztpraxis > Vorhandene Praxis live oeffnen** waehlen,
danach Play und **Simulation starten** anklicken. Die Szene verwendet das
vorhandene Patientenmodell und die Raum-Wegpunkte aus `SampleScene`;
Stationswechsel werden ueber die vorhandenen Tueren und den Mittelflur animiert.
Unity verwendet standardmaessig 6x; die HTML-Testseite verwendet weiterhin 60x.

Webbuild dieser Praxis: **Tools > Arztpraxis > Praxis-Live-Webbuild erstellen**,
anschliessend im SimSharp-Repository:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Copy-UnityWebBuild.ps1 -BuildPath C:\Users\marle\arztdemo\Builds\PraxisLive
```

Der bisherige Browserbuild wird erst beim Kopieren ersetzt.

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

`speed`: Simulationssekunden pro Echtzeitsekunde, 0.5 bis 600, Standard 60 (Unity-Bedienung: 0.5 bis 60).
Stoppen: `{"type":"stop"}`. Anschließend kann ein neuer Lauf gestartet werden.

Server sendet `ready`, `started`, `clock`, `event`, `completed`, `stopped` oder `error`.
Ereignisse enthalten `sessionId`, `runId`, fortlaufende `sequence`, `dayIndex: 0`
und `data` mit `zeit` (Minuten), `eventTyp`, `patientId`, `vonZustand`,
`zuZustand`, `arztId`, `schwesterId`. JSON-Eigenschaften sind camelCase.
Zusaetzlich enthaelt `data.bewegungsdauerMinuten` bei Bewegungsstarts die
exakte Dauer des folgenden SimSharp-Timeouts. Andere Ereignisse enthalten 0.
`ready.motionProtocolVersion: 1` kennzeichnet diese Erweiterung. Der Unity-Praxisclient
verlangt diese Kennung; nach einem Backend-Update deshalb den Server neu starten.
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

## Simulationsuhr und Geschwindigkeit

`ready.clockProtocolVersion: 1` kennzeichnet die Zeitsteuerung.
Waehrend eines Laufs kann der Client `{"type":"setSpeed","speed":0.5}` senden.
Ungueltige Werte ergeben `invalid_speed`, ausserhalb eines Laufs `not_running`.
Der Faktor wird sitzungsweise gewechselt; die bisher erreichte Zeit bleibt erhalten.

Der Server meldet etwa alle 100 ms (bei langer Ereignisverarbeitung spaeter):

```json
{"type":"clock","sessionId":"...","runId":"...","elapsedMinutes":12.5,"speed":6}
```

Die erste `clock`-Nachricht mit dem neuen Faktor bestaetigt den Wechsel.
Zeitmeldungen erhalten keine Patienten-Ereignissequenz. Vor `stopped`/`completed`
folgt eine letzte Zeitmeldung. Unity interpoliert bis zur bestaetigten Zeit,
niemals darueber hinaus, und verwendet dieselben Zeitschritte fuer Laufbewegungen.
Die Anzeige kann daher etwas hinter der Serverzeit liegen. Es gibt keinen
Rueckwaertsmodus und keine echte Pause/Fortsetzen-Funktion.
