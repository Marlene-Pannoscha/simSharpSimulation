# simSharpSimulation

## Projektordner öffnen

Wechsle zuerst in den C#-Projektordner:

```powershell
cd (c:\home\simSharpSimulation--deine Speicherordner)\simSharpSimulation
```

## Voraussetzungen

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

```powershell
dotnet run
```

## Anmerkungen zum Gesamtprogramm

- **Kennzahlen-Definitionen (für Auswertung):**
	- `Wartezeit Rezeption`: Zeit bis Start der Rezeption
	- `Wartezeit Schwester`: Zeit bis Start des Schwester-Prozesses
	- `Wartezeit Arzt`: Zeit bis Start der Arzt-Behandlung
	- `Gesamtprozesszeit`: Eintritt in die Klinik bis Verlassen der Klinik
- **Patienten-IDs** sind pro Tag eindeutig, damit Trace- und Zeitachsen-Auswertungen stabil bleiben.
- **Trace-Datei** wird als `klinik_trace.txt` im Projektordner gespeichert.
- **Diagramme** werden in `simSharpSimulation/images/` gespeichert.
- **Bewegungs-Events im Trace** folgen dem Muster:
	- `geht_*` = Start einer Bewegung
	- `betritt_*` = Ankunft am Ziel nach Bewegungszeit
	- Zeiten: Eingang→Rezeption 5s, interne Wege 10s, Arzt→Ausgang 15s, Rezeption→Ausgang 5s


