# simSharpSimulation

## Projektordner öffnen

Wechsle zuerst in den C#-Projektordner:

```powershell
cd c:\home\simSharpSimulation\simSharpSimulation
```

## Voraussetzungen

- Windows (für den WPF-Fenstermodus)
- .NET SDK (empfohlen: .NET 10)

## Pakete installieren (falls nötig)

Wenn die Pakete noch nicht vorhanden sind, installiere sie im Projektordner:

```powershell
dotnet add package SimSharp
dotnet add package MathNet.Numerics
dotnet add package ScottPlot
```

## Projekt bauen

```powershell
dotnet build
```

## Programm ausführen

### 1) Standard-Simulation (Konsole)

```powershell
dotnet run
```

### 2) Finanz-WPF (extra Fenster)

```powershell
dotnet run -- --finanz-wpf
```

### Alternativ von außerhalb des Projektordners

```powershell
dotnet run --project c:\home\simSharpSimulation\simSharpSimulation\simSharpSimulation.csproj -- --finanz-wpf
```

## Anmerkungen zum Gesamtprogramm

- **Kennzahlen-Definitionen (für Auswertung):**
	- `Wartezeit Rezeption`: Zeit bis Start der Rezeption
	- `Wartezeit Schwester`: Zeit bis Start des Schwester-Prozesses
	- `Wartezeit Arzt`: Zeit bis Start der Arzt-Behandlung
	- `Gesamtprozesszeit`: Eintritt in die Klinik bis Verlassen der Klinik
- **Patienten-IDs** sind pro Tag eindeutig, damit Trace- und Zeitachsen-Auswertungen stabil bleiben.
- **Trace-Datei** wird als `klinik_trace.txt` im Projektordner gespeichert.
- **Standard-Diagramme** werden in `simSharpSimulation/images/` gespeichert.
- **Finanz-Diagramme** (WPF) werden in `simSharpSimulation/Kosten/images/` gespeichert.
- **Mietkosten** in der Finanzansicht berechnen sich aus Schwesterzimmern, Arztzimmern und der Wartezimmerfläche.
- **Bewegungs-Events im Trace** folgen dem Muster:
	- `geht_*` = Start einer Bewegung
	- `betritt_*` = Ankunft am Ziel nach Bewegungszeit
	- Zeiten: Eingang→Rezeption 5s, interne Wege 10s, Arzt→Ausgang 15s, Rezeption→Ausgang 5s


