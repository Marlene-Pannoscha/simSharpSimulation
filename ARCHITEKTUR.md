# Architekturuebersicht - simSharpSimulation

Diese Datei dokumentiert die fachliche und technische Gesamtlogik des Projekts.
Sie ergaenzt die `README.md`: Die README beschreibt vor allem Bedienung und
Startbefehle, diese Datei erklaert Aufbau, Datenfluss und Verantwortlichkeiten
im Code.

Die aktuelle eigenstaendige Refactoring-Etappe und ihre Regressionsnachweise
sind in [EigenstaendigesRefactoring.md](docs/EigenstaendigesRefactoring.md)
dokumentiert.

## 1. Ziel des Projekts

`simSharpSimulation` simuliert den Patientenfluss in einer Arztpraxis bzw.
Klinik mit diskreter Ereignissimulation. Dabei werden Ankuenfte, Wartezeiten,
Behandlungen, Abbrueche, Prognosen und Finanzkennzahlen erfasst.

Die wichtigsten Ergebnisse sind:

- Trace-Datei mit Prozessereignissen
- Wartezeiten und Behandlungszeiten je Station
- Hit/Miss-Auswertung fuer behandelte und nicht behandelte Nachfrage
- Finanzbericht mit Umsatz, Kosten, Gewinn und Break-even
- Prognoseauswertung fuer Restzeiten und Abbruchgruende
- Diagramme als PNG-Dateien
- optionales WPF-Fenster mit interaktiver Auswertung

## 2. Projektstruktur

```text
simSharpSimulation/
|-- README.md
|-- ARCHITEKTUR.md
|-- klinik_trace.txt
|-- prognose_report.txt
|-- prognose_daten.json
|-- simSharpSimulation.slnx
|-- simSharpSimulation/
|   |-- Program.cs
|   |-- SimulationKonfiguration.cs
|   |-- SimulationsDaten.cs
|   |-- simSharpSimulation.csproj
|   |-- Ressourcen/
|   |   |-- KonfigurationJsonExport.cs
|   |   |-- PatientenKonfiguration.cs
|   |   |-- RezeptionKonfiguration.cs
|   |   |-- SchwesterKonfiguration.cs
|   |   |-- ArztKonfiguration.cs
|   |   |-- Personen.cs
|   |-- Prozess/
|   |   |-- README_Prozess.md
|   |   |-- PatientenGenerator.cs
|   |   |-- PatientenTyp.cs und PatientenBehandlungsplan.cs
|   |   |-- Aufnahmestopp.cs und Tagesressourcen.cs
|   |   |-- RezeptionPhase.cs
|   |   |-- SchwesterPhase.cs
|   |   |-- ArztPhase.cs
|   |   |-- BehandlungsPhaseErgebnis.cs
|   |   |-- PatientenProzess/
|   |       |-- PatientenProzess.cs
|   |       |-- Aufnahmeprognose.cs
|   |       |-- PatientenTagesablauf.cs
|   |       |-- PatientenTagesablauf.PatientenAblauf.cs
|   |       |-- PatientenTagesablauf.Prognose.cs
|   |       |-- PatientenTagesablauf.Helfer.cs
|   |-- Auswertung/
|   |   |-- FinanzRechner.cs
|   |   |-- PrognoseStatistik.cs und PrognoseStatistik.Export.cs
|   |-- SimulationsEreignisFormat.cs
|   |-- Diagramm/
|   |   |-- GenerateDiagram.cs
|   |   |-- einzelne Diagrammklassen
|   |-- WPF Fenster/
|   |   |-- FinanzWpfFenster.cs
|   |   |-- FinanzWpfFenster.SimulationsUebersicht.cs
|   |   |-- FinanzWpfFenster.HitMiss.cs
|   |   |-- FinanzWpfFenster.Wartezeiten.cs
|   |   |-- Kosten/
|   |   |   |-- FinanzVisualisierung.cs
|   |   |   |-- FinanzWpfFenster.Finanzen.cs
|   |   |-- Prognose/
|   |       |-- FinanzWpfFenster.Prognose.cs
|   |       |-- PrognoseVisualisierung.cs
|   |-- images/
```

`PraxisSimulation.Core` bindet die gemeinsamen Modellquellen ein und wird von
Godot, Server und dem .NET-Prüfprogramm referenziert. Die Desktopprojekte
kompilieren dieselben Quellen im bestehenden Projektlayout. Der Godot-Adapter
greift typisiert auf den Kern zu. Das Kernprojekt hängt von SimSharp und MathNet
ab. WPF, ScottPlot und Godot liegen außerhalb des Kerns.

## 3. Programmstart

Der Einstieg liegt in `Program.cs`.

### Konsolenmodus

Ohne Argumente startet das Programm die Simulation direkt in der Konsole:

1. JSON-Konfigurationen werden geladen.
2. `SimulationsDaten` wird als zentraler Datencontainer erzeugt.
3. `PatientenProzess.FuehreAus()` simuliert die Arbeitstage.
4. Standarddiagramme werden erzeugt.
5. `klinik_trace.txt`, `prognose_report.txt` und `prognose_daten.json` werden geschrieben.
6. Wartezeiten, Prognosebericht und Finanzkennzahlen werden in der Konsole ausgegeben.

### WPF-Modus

Mit `--finanz-wpf` wird statt der Konsolenauswertung das WPF-Fenster gestartet:

```powershell
dotnet run -- --finanz-wpf
```

Auch hier werden zuerst die JSON-Konfigurationen geladen. Danach baut
`FinanzWpfFenster` die Oberflaeche programmatisch in C# auf.

## 4. Simulationslogik

Die Prozesslogik liegt im Ordner `simSharpSimulation/Prozess`.

Der fachliche Patientenfluss ist:

1. Patient betritt die Klinik.
2. Patient geht zur Rezeption.
3. Rezeption wird durchlaufen.
4. Je nach Patiententyp folgt eine Schwesterphase.
5. Patient wartet auf den Arzt.
6. Arztbehandlung findet statt.
7. Optional geht der Patient zurueck zur Rezeption.
8. Patient geht zum Ausgang und verlaesst die Klinik.

Die Details der Prozesslogik sind zusaetzlich in
`simSharpSimulation/Prozess/README_Prozess.md` beschrieben.

### Wichtige Prozessdateien

- `PatientenGenerator.cs`: erzeugt Ankunftszeiten und startet Patientenprozesse.
- `PatientenProzess.cs`: orchestriert Tage, Ressourcen und SimSharp-Simulation.
- `PatientenTagesablauf.PatientenAblauf.cs`: beschreibt den Ablauf eines einzelnen Patienten.
- `PatientenTagesablauf.Prognose.cs`: enthaelt Prognoseformeln und prognosebasierte Abbrueche.
- `PatientenTagesablauf.Helfer.cs`: protokolliert den Ausgangspfad nach einem Abbruch.
- `PatientenBehandlungsplan`: zieht Dauern, Terminstatus und Pfade in der historischen Reihenfolge.
- `Aufnahmestopp` und `Aufnahmeprognose`: berechnen den Aufnahmestopp und verwalten die Zulassungsentscheidung je Tag.
- `Tagesressourcen`: erzeugt Personalpools, separate Raumressourcen und den Belegungsstatus fuer Prognosen.
- `RezeptionPhase.cs`: kapselt Warte- und Behandlungslogik der Rezeption.
- `SchwesterPhase.cs`: kapselt Schwesterlogik inklusive Prioritaet und Feierabend-Abbruch.
- `ArztPhase.cs`: kapselt Arztwarteschlange, Arztbehandlung und Hit-Erfassung.

## 5. Ressourcen und Konfiguration

Die fachlichen Parameter liegen im Ordner `Ressourcen`.

- `KonfigurationJsonExport.cs` laedt JSON-Dateien und uebertraegt Werte in die statischen Konfigurationen.
- `SimulationKonfiguration.cs` enthaelt globale Simulationswerte wie Seed, Tagesdauer und Wegezeiten.
- `PatientenKonfiguration.cs` enthaelt Ankunftsverteilung, Patiententypen, Terminanteile und Wartezimmerannahmen.
- `RezeptionKonfiguration.cs`, `SchwesterKonfiguration.cs`, `ArztKonfiguration.cs` enthalten Kapazitaeten und Servicezeiten.

Im WPF-Fenster koennen mehrere Werte zur Laufzeit angepasst werden:

- Anzahl Aerzte
- Anzahl Schwestern
- Anzahl Rezeptionisten
- Anzahl und Flaechen der Arzt- und Schwesterzimmer
- Wartezimmerflaeche
- Infrastrukturkosten pro Tag
- Geraete-Leasing pro Tag
- Finanzzeitraum

Diese Eingaben werden vor der Simulation validiert und dann in die globalen
Konfigurationswerte uebernommen.
Die Raum- und Flaechenwerte werden im Tab `Konfiguration` bearbeitet. Dort
lassen sich die Anzahl der Arzt- und Schwesterzimmer, die Flaechen je Zimmer
und die Wartezimmerflaeche anpassen.

## 6. Datensammlung

`SimulationsDaten.cs` ist die zentrale Sammelstelle fuer alle Laufzeitdaten.

Gespeichert werden unter anderem:

- echte Ankunftszeiten
- Trace-Events
- Wartezeiten je Station
- Behandlungszeiten je Station
- Gesamtprozesszeiten
- Daten getrennt nach Patienten mit und ohne Termin
- Patiententyp-Verteilung
- Hit/Miss-Werte pro Tag
- nicht behandelte Patienten und Abbruchgruende
- Prognosepruefungen und Prognoseabbrueche

Aus diesen Daten werden Konsolenausgaben, WPF-Tabellen, Reports und Diagramme
erzeugt.

`PrognoseStatistik` verwaltet Prognosedaten, Genauigkeit und Kalibrierung.
`PrognoseStatistik.Export.cs` erzeugt den bestehenden Textbericht und JSON-Export.
Aufrufer nutzen dafür weiterhin `SimulationsDaten`.

## 7. Trace und Events

`SimulationsEreignisFormat` lädt das Zustandsmapping und erzeugt aus einem
Ereignis die Trace-Zeile und die Meldung für Godot bzw. den Server.
`SimulationsDaten` speichert zuerst die Trace-Zeile und veröffentlicht dann die
Meldung.

Der Trace wird als Semikolon-getrennte Textdatei geschrieben:

```text
Zeit;EventTyp;VonZustand;ZuZustand;PatientId;ArztId;SchwesterId
```

Bewegungen folgen einem einfachen Muster:

- `geht_*`: Start einer Bewegung.
- `betritt_*`: Ankunft am Ziel nach Bewegungszeit.

Aktuelle Wegezeiten:

- Eingang zu Rezeption: 5 Sekunden
- interne Wege: 10 Sekunden
- Arzt zu Ausgang: 15 Sekunden
- Rezeption zu Ausgang: 5 Sekunden

Die Patienten-IDs sind pro Tag eindeutig, damit Trace- und Zeitachsen-
Auswertungen stabil bleiben.

## 8. Diagramme

Der Ordner `Diagramm` erzeugt die Standarddiagramme. `GenerateDiagram.cs`
koordiniert die einzelnen Diagrammklassen.

Typische Ausgaben sind:

- Ankunftsverteilung Simulation vs. Theorie
- PDF/CDF der Ankunftsverteilung
- Behandlungszeiten Arzt und Schwester
- Wartezeiten-Histogramme
- Wartezeitenvergleich Arzt/Schwester
- Patienten-Zeitachsen
- Hit/Miss pro Tag

Die PNG-Dateien werden unter `simSharpSimulation/images/` gespeichert.

## 9. Finanzlogik

`Auswertung/FinanzRechner.cs` enthält die Finanzrechnung. Berichte, Diagramme und die WPF-Anbindung liegen in `WPF Fenster/Kosten`.

- `FinanzRechner.cs`: berechnet Tagesergebnisse, Kosten, Umsaetze und Fixkosten.
- `FinanzVisualisierung.cs`: fuehrt Finanzsimulationen aus, erzeugt Finanzberichte und Diagramme.
- `FinanzWpfFenster.Finanzen.cs`: baut den Finanzen-Tab und zeigt Textbericht, Break-even und Diagramme.

Die Finanzsicht nutzt unter anderem:

- Personalkosten
- Mietkosten aus Raeumen und Flaechen
- Infrastrukturkosten
- Geraete-Leasing
- Behandlungskosten
- Versicherungs- und Behandlungsmix
- saisonale Gewinnanteile
- Break-even-Berechnung

Die Mietkennzahlen im WPF-Eingabebereich werden nach einer Simulation
aktualisiert.

## 10. WPF-Fenster

`FinanzWpfFenster` ist eine `partial class`. Das Fenster wird ohne XAML direkt
in C# aufgebaut.

### Hauptdatei

`FinanzWpfFenster.cs` enthaelt:

- Fenstergrundlayout
- kompakter Eingabebereich fuer Personal, Zeitraum, Startbutton und Raum-/Kostenuebersicht
- Konfiguration-Tab fuer die Detailwerte zu Raeumen, Flaechen und Fixkosten
- Startbutton und Eventhandler
- zentrale UI-Helfer fuer Textboxen, Bildcontainer, Parametergruppen und Tabs
- Laden von PNG-Dateien in WPF-Images

### Tabs

- `FinanzWpfFenster.SimulationsUebersicht.cs`
  - zeigt kompakte Simulationskennzahlen und Tagesfinanzen.
- `FinanzWpfFenster.cs`
  - enthaelt den Konfiguration-Tab fuer Raeume, Flaechen, Infrastruktur und Leasing.
- `FinanzWpfFenster.Finanzen.cs`
  - zeigt Finanzbericht, Break-even-Anzeige und Diagramme.
- `FinanzWpfFenster.HitMiss.cs`
  - zeigt behandelte vs. nicht behandelte Nachfrage.
- `FinanzWpfFenster.Wartezeiten.cs`
  - zeigt Wartezeitenbericht und Tabellen.
- `FinanzWpfFenster.Prognose.cs`
  - zeigt Prognosebericht und Prognosediagramme.

### Wartezeiten-Tab

Der Wartezeiten-Tab berechnet aus Trace-Events mehrere Tabellen:

- Warteschlangen: Patientenanzahl
- Auslastung
- Bereiche: Patientenanzahl
- Wartezeiten je Warteschlange
- Behandlungszeit: Ist vs. Erwartet

Die Tabellenlogik rekonstruiert aus Trace-Events zeitgewichtete Patientenanzahlen
und Wartezeiten. Die UI-Tabellen wurden bewusst in der alten kompakten
Darstellung belassen.

## 11. Prognoselogik

Die Prognose bewertet an mehreren Checkpoints, ob ein Patient voraussichtlich
noch vor Schichtende fertig wird.

Checkpoints sind:

- Ankunft
- NachRezeption
- VorSchwester
- NachSchwester
- VorArzt
- NachArzt

`PatientenTagesablauf.Prognose.cs` berechnet erwartete Restzeiten aus mittleren
Service- und Wegezeiten. Die Prognose berücksichtigt vorab gezogene Dauern sowie aktuelle und erwartete Warteschlangen. `PrognoseStatistik` gleicht prognostizierte mit tatsächlichen Restzeiten ab und aktualisiert die Kalibrierung.

Ausgaben:

- `prognose_report.txt`
- `prognose_daten.json`
- Prognosediagramme unter `WPF Fenster/Prognose/images/`

`PrognoseVisualisierung.cs` erstellt Diagramme zur Trefferquote, Restzeit und
Abbruchgruenden.

## 12. Output-Artefakte

Wichtige erzeugte Dateien:

- `klinik_trace.txt`
- `prognose_report.txt`
- `prognose_daten.json`
- `simSharpSimulation/images/*.png`
- `simSharpSimulation/WPF Fenster/Prognose/images/*.png`

Im WPF-Modus werden die Finanz-, Hit/Miss-, Wartezeiten- und Prognoseansichten
nach dem Start einer Simulation aktualisiert.

## 13. Externe Abhaengigkeiten

Aus `simSharpSimulation.csproj`:

- `SimSharp`: diskrete Ereignissimulation
- `MathNet.Numerics`: Verteilungen und Zufallsziehungen
- `ScottPlot`: Diagrammerzeugung

Das Projekt nutzt `net10.0-windows` mit `UseWPF=true`.

## 14. Erweiterungspunkte

Typische Erweiterungen:

- neue Station als eigene Phase, z. B. Labor
- neue KPIs in `SimulationsDaten`
- neue Diagramme im Ordner `Diagramm`
- weitere WPF-Tabs als eigene Partial-Dateien
- queue-sensitive Prognose statt Mittelwertmodell
- CSV- oder JSON-Exports fuer Tabellen
- Szenariovergleiche fuer unterschiedliche Personal- und Raumkonfigurationen

## 15. Pflegehinweise

- Prozesslogik in den jeweiligen Phasen bzw. in `PatientenProzess` halten.
- Konfigurationswerte zentral ueber Ressourcen-/JSON-Konfigurationen pflegen.
- Neue Messwerte zuerst in `SimulationsDaten` aufnehmen.
- Diagrammcode getrennt von Prozesslogik halten.
- WPF-UI-Erweiterungen bevorzugt in eigenen Partial-Dateien ergaenzen.
- Bei neuen Kennzahlen README und diese Architekturdatei aktualisieren.


## 16. Optionale Simulationsparameter

`simulation-konfiguration.json` akzeptiert diese zusätzlichen Felder. Fehlen
sie in älteren Dateien, gelten die bisherigen Werte.

| Feld | Standard |
|---|---:|
| `PrognosePruefungVorSchliessungMinuten` | 60 |
| `NachlaufMinuten` | 180 |
| `WahrscheinlichkeitNachArztZurRezeption` | 0,6 |

Personalstärken werden pro Rolle gelesen und an die Personalpools bzw. die
Rezeptionsressource übergeben. Arzt- und Schwesternräume haben separat
konfigurierte Kapazitäten aus `Finanzen.Fixkosten`. Diese Werte ändern sich
unabhängig voneinander. Für die Rezeption gibt es im Kern eine Personalressource.

## 17. Reihenfolge des Refactorings und offene Punkte

1. Referenzlaeufe vor der ersten Modellaenderung sichern. Ereignisreihenfolge,
   exakte Zeiten und Auswertungen zeigen auch Abweichungen bei gleichen Fallzahlen.
2. Tagessteuerung und Ressourcen zuerst trennen. So ist sichtbar, welche
   Kapazitaeten je Tag gelten und wo ein neuer Patientenprozess gestartet wird.
3. Behandlungsplan und Aufnahmeprognose auslagern. Die Reihenfolge der
   Zufallsziehungen und die Reihenfolge bei gleichen Prognosewerten bleiben
   als Invarianten im Code erkennbar.
4. Prognosestatistik, Export und Ereignisformat aus dem Ergebnisobjekt
   herausziehen. Dessen vorhandene API bleibt der Zugang fuer UI und Reports.
5. Godot typisiert anbinden und die getrennten Kapazitaeten im Adapter testen.

Die globale, aenderbare Konfiguration koppelt parallele Simulationen mit
verschiedenen Parametern. Ein unveraenderlicher Satz von Parametern je Lauf
waere eine Grundlage fuer parallele Szenariovergleiche. Der Patientenablauf
bleibt ein laengerer SimSharp-Iterator mit fachlich relevanter Reihenfolge.
Weitere Extraktionen sollten Stationsgrenzen nutzen und die Taktung gegen
die Referenzlaeufe pruefen. Der Mitarbeiterpool liest die Queue-Laenge ueber
SimSharp-Interna. Dieser Zugriff ist bei einem Paketwechsel zu pruefen.
