# simSharpSimulation

Simulation einer Arztpraxis mit SimSharp. Das Projekt kann als Konsolenprogramm
oder mit einem zusaetzlichen WPF-Fenster fuer Finanz-, Wartezeiten- und
Prognoseauswertungen gestartet werden.

## Projektordner oeffnen

Wechsle zuerst in den C#-Projektordner:

```powershell
cd c:\uni\simSharpSimulation\simSharpSimulation
```

## Voraussetzungen

- Windows fuer den WPF-Fenstermodus
- .NET SDK, empfohlen: .NET 10
- NuGet-Pakete aus der Projektdatei:
  - `SimSharp`
  - `MathNet.Numerics`
  - `ScottPlot`

## Pakete installieren

Normalerweise reicht ein Restore/Build, weil die Pakete in der `.csproj`
eingetragen sind:

```powershell
dotnet restore
```

Falls Pakete manuell nachinstalliert werden muessen:

```powershell
dotnet add package SimSharp
dotnet add package MathNet.Numerics
dotnet add package ScottPlot
```

## Projekt bauen

```powershell
dotnet build
```

## Programm ausfuehren

### Standard-Simulation in der Konsole

```powershell
dotnet run
```

### Vollstaendige Simulation mit Diagrammen und Reports

```powershell
dotnet run -- --mit-images
```

Dieser Modus fuehrt die Simulation aus und erzeugt anschliessend Diagramme, Reports und Bilddateien. Das entspricht dem vollstaendigen Standardlauf, ist aber als eigener Befehl benannt.

### Nur reine Simulationszeit messen

```powershell
dotnet run -- --nur-simulationszeit
```

Dieser Modus fuehrt nur die Simulation aus und erzeugt keine Diagramme, Reports oder Bilddateien.

### Finanz- und Auswertungsfenster starten

```powershell
dotnet run -- --finanz-wpf
```

### Alternativ von ausserhalb des Projektordners

```powershell
dotnet run --project c:\uni\simSharpSimulation\simSharpSimulation\simSharpSimulation.csproj -- --finanz-wpf
```

## WPF-Fenster

Das WPF-Fenster wird programmatisch in C# aufgebaut und enthaelt mehrere Tabs:

- `Uebersicht`: kompakte Simulationskennzahlen, Wartezeiten, Patienten-Typen und Tagesfinanzen.
- `Konfiguration`: Detailwerte fuer Raeume, Flaechen, Infrastruktur und Leasing.
- `Finanzen`: Umsatz, Kosten, Gewinn, Break-even-Anzeige und Diagramme.
- `Hit/Miss Analyse`: behandelte und nicht behandelte Nachfrage inklusive Diagramm.
- `Wartezeiten`: Textauswertung sowie Tabellen fuer Warteschlangen, Auslastung, Bereiche, Wartezeiten und Behandlungszeiten.
- `Prognose`: Prognosebericht und Diagramme zu Trefferquote, Restzeit und Abbruchgruenden.

Im oberen Eingabebereich bleiben Personal, Zeitraum, Startbutton und eine
kompakte Raum-/Kostenuebersicht sichtbar. Die Detailwerte fuer Raeume,
Flaechen, Infrastruktur und Geraete-Leasing liegen im Tab `Konfiguration`.
Dort koennen die Anzahl der Raeume, die Flaechen je Raum und die
Wartezimmerflaeche angepasst werden. Die berechneten Mietkennzahlen werden
direkt im Fenster aktualisiert.

## Ausgaben und Dateien

- `klinik_trace.txt`: Trace-Datei der Simulation.
- `prognose_report.txt`: Textbericht fuer die Prognoseauswertung.
- `prognose_daten.json`: Datengrundlage fuer die Prognosediagramme.
- `simSharpSimulation/images/`: Standard-Diagramme der Simulation.
- `simSharpSimulation/WPF Fenster/Prognose/images/`: Prognose-Diagramme.

Finanz- und weitere Diagramme werden beim Starten der Simulation aus dem WPF-
Fenster aktualisiert und anschliessend in den Tabs angezeigt.

## Kennzahlen

- `Wartezeit Rezeption`: Zeit bis Start der Rezeption.
- `Wartezeit Schwester`: Zeit bis Start des Schwester-Prozesses.
- `Wartezeit Arzt`: Zeit bis Start der Arzt-Behandlung.
- `Gesamtprozesszeit`: Eintritt in die Klinik bis Verlassen der Klinik.
- `Hit`: Patient konnte behandelt werden.
- `Miss`: Patient konnte wegen begrenzter Tageskapazitaet nicht behandelt werden.

Die Patienten-IDs sind pro Tag eindeutig, damit Trace- und Zeitachsen-
Auswertungen stabil bleiben.

## Trace-Events

Bewegungs-Events im Trace folgen diesem Muster:

- `geht_*`: Start einer Bewegung.
- `betritt_*`: Ankunft am Ziel nach Bewegungszeit.

Aktuelle Wegezeiten:

- Eingang zu Rezeption: 5 Sekunden
- interne Wege: 10 Sekunden
- Arzt zu Ausgang: 15 Sekunden
- Rezeption zu Ausgang: 5 Sekunden

