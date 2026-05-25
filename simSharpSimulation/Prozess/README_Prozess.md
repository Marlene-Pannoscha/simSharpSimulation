# README Prozess

Diese Datei dokumentiert die Ablauf- und Prognoselogik im Ordner `simSharpSimulation/Prozess`.

## Überblick

Der Prozessbereich ist in zwei Ebenen aufgeteilt:

- `PatientenGenerator.cs` erzeugt die Patientenankünfte eines Tages.
- der Unterordner `PatientenProzess/` enthält die aufgeteilte `partial class PatientenProzess`.
- `RezeptionPhase.cs`, `SchwesterPhase.cs` und `ArztPhase.cs` kapseln die stationsspezifische Wartelogik, Behandlung und Feierabend-Abbrüche.
- `BehandlungsPhaseErgebnis.cs` dient als einfacher Rückkanal, damit eine Phase dem Hauptprozess melden kann, dass der Patient die Klinik bereits verlassen hat.

Die Prognoselogik sitzt aktuell zentral im Unterordner `PatientenProzess/`, fachlich aber weiterhin in derselben Klasse `PatientenProzess`. Die Stationsklassen berechnen selbst keine Prognosen, sondern schließen nur offene Prognoseprüfungen, wenn ein Patient wegen Feierabend die Klinik verlassen muss.

## Dateien und Verantwortung

### `PatientenGenerator.cs`

Verantwortung:

- erzeugt pro Tag bis zu `PatientenKonfiguration.ANZAHL_PATIENTEN_TAG` Ankunftszeitpunkte
- verwendet eine Normalverteilung mit `ERWARTUNGSWERT` und `STANDARDABWEICHUNG`
- verwirft Ankünfte nach `SimulationKonfiguration.SIMULATIONSDAUER`
- sortiert alle Ankünfte chronologisch
- behandelt Patienten vor Öffnung explizit als FIFO-Warteschlange
- startet für jede Ankunft einen separaten Patientenprozess mit `env.Process(...)`

Wichtig:

- Der Generator entscheidet nur, wann Patienten eintreffen.
- Die gesamte fachliche Logik ab Klinik-Eintritt liegt im Unterordner `PatientenProzess/`.

### `PatientenProzess/PatientenProzess.cs`

Verantwortung:

- Konstruktor der Klasse `PatientenProzess`
- Tages-Orchestrierung in `FuehreAus()`
- Erzeugung von SimSharp-Ressourcen pro Tag
- Start des `PatientenGenerator`
- Steuerung von Tagesstart, Tagesende und Nachlaufpuffer

### `PatientenProzess/PatientenProzess.PatientenAblauf.cs`

Verantwortung:

- kompletter End-to-End-Ablauf eines einzelnen Patienten
- Entscheidung über Terminpfad, Schwesterpfad, Arztpfad und Rückweg zur Rezeption
- Einbettung aller Prognose-Checkpoints in den fachlichen Ablauf
- Abschluss des Patienten mit `verlaesst_klinik` und Gesamtprozesszeit

### `PatientenProzess/PatientenProzess.Prognose.cs`

Verantwortung:

- Prognose-Checkpoints
- Prognose-Abbruchlogik
- Berechnung der erwarteten Restzeiten
- Hilfsformeln für Schwester-, Arzt- und Ausgangspfad

### `PatientenProzess/PatientenProzess.Helfer.cs`

Verantwortung:

- Auswahl freier oder zufälliger Ressourcen
- Wahl des `PatientenTyp`
- technische Hilfsmethoden zur Belegungsprüfung

### `BehandlungsPhaseErgebnis.cs`

Verantwortung:

- transportiert nur ein Flag: `PatientHatKlinikVerlassen`
- wird von Stationsphasen gesetzt, wenn dort ein endgültiger Abbruch passiert
- verhindert, dass `PatientenProzess` nach einer bereits beendeten Phase versehentlich weiterläuft

## Hauptfluss in `PatientenProzess/`

### Tagessteuerung

`PatientenProzess/PatientenProzess.cs` enthält `FuehreAus()` und erzeugt für jeden simulierten Arbeitstag:

- eine neue `Simulation`
- die Arzt-Ressourcen als `PriorityResource`
- die Schwester-Ressourcen als `PriorityResource`
- die Rezeption als `Resource`

Danach startet der Generator den Tagesfluss. Jeder Tag läuft bis `SIMULATIONSDAUER + 180 Minuten Nachlaufpuffer`.

### Patientenfluss

`PatientenProzess/PatientenProzess.PatientenAblauf.cs` enthält den normalen Pfad eines Patienten:

1. `betritt_klinik`
2. Bewegung zur Rezeption
3. Rezeption
4. optional Schwester-Vorbereitung
5. Wartezimmer für Arzt
6. Arzt
7. optional Rückweg zur Rezeption
8. Ausgang
9. `verlaesst_klinik`

Zusätzlich werden früh festgelegt:

- `PatientenTyp` über `PatientenKonfiguration.TYPEN_VERTEILUNG`
- `hatTermin`

Diese beiden Werte beeinflussen:

- Priorität bei Schwester und Arzt
- mittlere Behandlungsdauer
- Wartezimmerdauern
- Prognose der Restlaufzeit

## Prognosemodell

### Grundidee

Das Prognosemodell schätzt an mehreren Punkten die verbleibende Restzeit bis zum Verlassen der Klinik.

Jede Prüfung speichert:

- Patient-ID
- Phase
- Prüfzeitpunkt
- prognostizierte Restminuten
- Boolean `PrognoseFertigBisSchichtende`

Die Speicherung erfolgt über `SimulationsDaten.ErfassePrognosePruefung(...)`.

Die spätere Auswertung erfolgt über `SimulationsDaten.SchliessePrognosen(...)`, sobald der Patient die Klinik tatsächlich verlassen hat.

### Aktuelle Checkpoints

Die Prognose wird in `PatientenProzess/PatientenProzess.PatientenAblauf.cs` an diesen Phasen ausgelöst:

- `Ankunft`
- `NachRezeption`
- `VorSchwester`
- `NachSchwester`
- `VorArzt`
- `NachArzt`

### Berechnungsprinzip

Die Restzeit wird aktuell aus Mittelwerten zusammengesetzt, nicht aus exakten Queue-Längen.

Verwendet werden insbesondere:

- Bewegungszeiten aus `SimulationKonfiguration`
- mittlere Rezeptionszeit aus `RezeptionKonfiguration`
- mittlere Schwester-Behandlungszeit aus `SchwesterKonfiguration`
- mittlere Arzt-Behandlungszeit aus `ArztKonfiguration`
- mittlere Wartezimmerzeiten und Terminfaktoren aus `PatientenKonfiguration`
- Pfadwahrscheinlichkeit `WahrscheinlichkeitNachArztZurRezeption = 0.6`

Die Formeln liegen in `PatientenProzess/PatientenProzess.Prognose.cs`.

Hilfsmethoden der Prognose:

- `BerechneErwarteteSchwesterRestzeit(...)`
- `BerechneSchwesterRestzeitNachRezeption(...)`
- `BerechneRestzeitAbSchwester(...)`
- `BerechneErwarteteRestzeitNachArzt(...)`
- `BerechneRestzeitNachArztMitKonkretemPfad(...)`
- `BerechneMittlereSchwesterWartezimmerzeit(...)`
- `BerechneMittlereArztWartezimmerzeit()`

### Was die Prognose aktuell nicht berücksichtigt

Noch nicht enthalten:

- konkrete Queue-Länge an Rezeption, Schwester oder Arzt
- genaue Restarbeitszeit anderer bereits wartender Patienten
- tatsächliche Ressourcenauslastung über mehrere Ressourcen hinweg
- konkrete Warteposition des Patienten in der Schlange

Deshalb ist das Modell aktuell ein mittelwertbasiertes Flussmodell, kein queue-genaues Vorhersagemodell.

## Prognosebasierter Abbruch

### Auslöser

`ErfassePrognoseCheckpoint(...)` gibt `true` oder `false` zurück:

- `true`: Prognose sagt, der Patient wird voraussichtlich vor Schichtende fertig
- `false`: Prognose sagt, der Patient wird voraussichtlich nicht vor Schichtende fertig

Wenn `false` zurückkommt, beendet `PatientenProzess/PatientenProzess.PatientenAblauf.cs` den Ablauf sofort mit `BrichWegenPrognoseAb(...)` aus `PatientenProzess/PatientenProzess.Prognose.cs`.

### Verhalten bei Prognose-Abbruch

`BrichWegenPrognoseAb(...)` macht aktuell:

1. `daten.ErfassePrognoseAbbruch(env.StartDate)`
2. `geht_zum_ausgang`
3. Bewegung zum Ausgang mit einer phasenspezifischen Wegzeit
4. `verlaesst_klinik`
5. `daten.SchliessePrognosen(patientId, nowMinutes)`

Wichtig:

- Prognose-Abbrüche werden bewusst nicht als eigenes Trace-Event wie `prognose_abbruch` geloggt.
- Im Trace sieht man daher nur den normalen Ausgangspfad.
- Die Zählung erfolgt ausschließlich in `SimulationsDaten`.

### Verwendete Wegzeiten beim Prognose-Abbruch

Je nach Prozesspunkt wird eine andere Ausgangswegzeit verwendet:

- bei `Ankunft`: `TimeSpan.Zero`
- nach Rezeption: `rezeptionZumAusgangDauer`
- vor oder nach Schwester: `interneBewegungsdauer`
- vor Arzt: `interneBewegungsdauer`
- nach Arzt: `arztZumAusgangDauer`

Das ist eine vereinfachte Annahme über den nächstliegenden realistischen Ausgangspfad.

## Rezeption: `RezeptionPhase.cs`

### Verantwortung

Die Rezeption kapselt:

- Eintritt in die Rezeptionswarteschlange
- Prüfung, ob Rezeption frei ist
- Warten bis Ressource frei oder Feierabend erreicht ist
- Durchführung der Rezeptionsbehandlung
- Erfassung von Warte- und Behandlungszeit
- Feierabend-Abbruch in der Rezeptionswarteschlange

### Wichtige Logik

- `IstRezeptionFrei(...)` prüft die aktuelle interne Belegung der Ressource per Reflection auf `Users`.
- Wenn kein Platz frei ist, wartet der Patient auf `rezeption.WhenAny()` oder auf `schichtEnde`.
- Die Behandlungsdauer ist lognormalverteilt um `MITTELREZEPTIONSZEIT`.

### Bezug zur Prognose

Die Rezeption selbst erzeugt keine Prognose.

Sie hat aber zwei wichtige Berührungspunkte:

- Nach der ersten Rezeption folgt im Hauptprozess aus `PatientenProzess/PatientenProzess.PatientenAblauf.cs` der Checkpoint `NachRezeption`.
- Wenn ein Patient wegen Feierabend in der Rezeptionswarteschlange abbricht, ruft die Phase `daten.SchliessePrognosen(...)` auf.

## Schwester: `SchwesterPhase.cs`

### Verantwortung

Die Schwesterphase kapselt:

- Eintritt in Schwester-Warteschlange oder Direktweg
- Warten auf freie Schwester oder auf Feierabend
- Prioritätsvergabe nach `PatientenTyp`
- Bewegung ins Schwesterzimmer
- Schwesterbehandlung
- Feierabend-Abbruch in der Schwesterwarteschlange

### Wichtige Logik

- `GetPriority(...)` priorisiert kurze Fälle vor mittleren und langen Fällen.
- `PriorityResource` wird mit dieser Priorität angefragt.
- Die Schwester-Behandlungsdauer ist lognormalverteilt auf Basis des zum `PatientenTyp` gehörenden Mittelwerts.

### Bezug zur Prognose

Die Schwesterphase selbst rechnet keine Prognose aus.

Die Prognose wird im Hauptprozess aus `PatientenProzess/PatientenProzess.PatientenAblauf.cs` um die Schwesterphase herum gesetzt:

- `VorSchwester`
- `NachSchwester`

Wenn die Schwesterphase wegen Feierabend abbricht, schließt sie alle offenen Prognoseprüfungen.

## Arzt: `ArztPhase.cs`

### Verantwortung

Die Arztphase kapselt:

- Warten auf freien Arzt
- Priorisierung nach `PatientenTyp`
- Feierabend-Abbruch aus der Arztwarteschlange
- Bewegung zum Arzt
- Arztbehandlung
- Erfassung von Hit, Wartezeit und Behandlungsdauer

### Wichtige Logik

- Auch hier wird `PriorityResource` verwendet.
- Der Patient gilt als `Hit`, sobald der Request erfolgreich war und die Behandlung beginnt.
- Die Arztbehandlung darf nach Schichtende noch zu Ende geführt werden, wenn der Patient den Arzt bereits erreicht hat.
- Abgebrochen wird nur, solange der Patient noch wartet.

### Bezug zur Prognose

Die Arztphase rechnet ebenfalls keine Prognose selbst.

Die Prognosepunkte davor bzw. danach liegen im Hauptprozess aus `PatientenProzess/PatientenProzess.PatientenAblauf.cs`:

- `VorArzt`
- `NachArzt`

Wenn der Patient die Arztwarteschlange wegen Feierabend verlassen muss, schließt die Arztphase offene Prognoseprüfungen sauber ab.

## Zusammenspiel zwischen Hauptprozess und Phasen

Die Trennung ist bewusst so gewählt:

- `PatientenProzess/PatientenProzess.PatientenAblauf.cs` entscheidet den fachlichen Gesamtpfad
- `PatientenProzess/PatientenProzess.Prognose.cs` bündelt die Prognoseformeln und den Prognose-Abbruch
- Stationsphasen entscheiden Ressourcenzuteilung, Warten, Behandlung und Feierabend-Abbruch

Dadurch bleibt die Prognose an einer Stelle gebündelt und die Stationslogik unabhängig von Prognoseformeln.

## Aktuelle Grenzen der Prozessdokumentation

Wichtig für die weitere Pflege:

- Die Prognose ist derzeit mittelwertbasiert, nicht queue-sensitiv.
- Die Phasenklassen kennen keine Prognose, sondern nur ihre Warte- und Abbruchlogik.
- `prognose_abbruch` ist im Zustandsmapping vorhanden, wird aber aktuell bewusst nicht als Trace-Event geschrieben.

## Gute nächste Erweiterungen

Wenn die Prozesslogik weiterentwickelt wird, sind diese nächsten Schritte sinnvoll:

- queue-bewusste Prognose auf Basis realer Warteschlangen
- explizites Trace-Event für Prognose-Abbruch, falls fachlich gewünscht
- Dokumentation der Prognoseformeln auch im Architektur-README
- Vergleich von prognosebasiertem Abbruch gegen reinen Feierabend-Abbruch in separaten Reports
