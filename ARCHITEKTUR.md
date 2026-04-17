# Architekturübersicht – simSharpSimulation

Diese Datei dokumentiert die Struktur und den Ablauf des Projekts `simSharpSimulation`.
Sie ergänzt die `README.md` um eine fachliche und technische Übersicht.

## 1) Ziel des Projekts

Das Projekt simuliert den Patientenfluss in einer Klinik über eine Arbeitswoche (Montag bis Freitag).
Dabei werden Wartezeiten und Prozessereignisse erfasst, als Trace gespeichert und als Diagramme visualisiert.

**Hauptziele:**

- Simulation von Ankunft, Rezeption, Schwester und Arzt
- Messung von Wartezeiten je Station
- Export von Ereignissen in `klinik_trace.txt`
- Erzeugung von Diagrammen unter `images/`

## 2) Projektstruktur

```text
simSharpSimulation/
├─ README.md
├─ ARCHITEKTUR.md
├─ klinik_trace.txt
├─ simSharpSimulation.slnx
├─ simSharpSimulation/
│  ├─ Program.cs
│  ├─ SimulationKonfiguration.cs
│  ├─ SimulationsDaten.cs
│  ├─ simSharpSimulation.csproj
│  ├─ Simulation/
│  │  ├─ Prozess/
│  │  │  ├─ PatientenProzess.cs
│  │  │  └─ PatientenGenerator.cs
│  │  ├─ Ressourcen/
│  │  │  ├─ Personen.cs
│  │  │  ├─ PatientenKonfiguration.cs
│  │  │  ├─ RezeptionKonfiguration.cs
│  │  │  ├─ SchwesterKonfiguration.cs
│  │  │  └─ ArztKonfiguration.cs
│  │  └─ Diagramm/
│  │     └─ GenerateDiagramms.cs
│  ├─ bin/
│  ├─ obj/
│  └─ images/
└─ sim_py/
```

## 3) Verantwortlichkeiten der wichtigsten Dateien

### Einstieg und Orchestrierung

- `Program.cs`
  - Startet die Simulation
  - Initialisiert `SimulationsDaten` und `PatientenProzess`
  - Ruft Diagrammerzeugung auf
  - Schreibt `klinik_trace.txt`
  - Gibt Kennzahlen (Durchschnittswartezeiten) in der Konsole aus

### Globale Einstellungen

- `SimulationKonfiguration.cs`
  - Legt zentrale Simulationsparameter fest (Seed, Simulationsdauer)

### Datensammlung und Auswertung

- `SimulationsDaten.cs`
  - Sammelt Trace-Events
  - Speichert Wartezeiten (Rezeption, Schwester, Arzt)
  - Berechnet Durchschnittswerte über Properties

### Prozesslogik

- `Prozess/PatientenProzess.cs`
  - Kernablauf der Simulation über 5 Tage
  - Verwaltet Ressourcen (Rezeption, Schwester, Arzt)
  - Definiert den Weg eines Patienten durch die Stationen
- `Prozess/PatientenGenerator.cs`
  - Erzeugt Ankunftszeiten pro Tag
  - Startet Patient-Prozesse in der Simulationsumgebung
- `Prozess/RezeptionPhase.cs`
  - Enthält ausschließlich Rezeption-Logik
- `Prozess/SchwesterPhase.cs`
  - Enthält Schwester-Logik inkl. Vorbereitungspfade
- `Prozess/ArztPhase.cs`
  - Enthält Arzt-Logik und Behandlungsdauer

### Ressourcen- und Fachkonfiguration

- `Ressourcen/Personen.cs`
  - Basisklasse für gemeinsame Ressourcenmerkmale
- `Ressourcen/*Konfiguration.cs`
  - Konstante Parameter zu Kapazität und Servicezeiten
  - Beispiele:
    - `ArztKonfiguration`: Anzahl Ärzte, mittlere Behandlungszeit
    - `SchwesterKonfiguration`: Anzahl Schwestern, mittlere Schwesterzeit
    - `RezeptionKonfiguration`: Anzahl Rezeptionisten, mittlere Rezeptionszeit
    - `PatientenKonfiguration`: Ankunftsverteilung, Terminwahrscheinlichkeiten, Wartezimmerdauer

### Visualisierung

- `Diagramm/GenerateDiagram.cs`
  - Erzeugt Diagramme auf Basis der Simulationsdaten
  - Speichert Ergebnisse in `images/`

## 9) Anmerkungen & Qualitätscheck

- **Stärken:**
  - Klare Trennung von Ablauf (`Prozess`), Parametern (`Ressourcen`) und Darstellung (`Diagramm`).
  - `SimulationsDaten` als zentrale Sammelstelle vereinfacht KPI-Auswertung.
  - Gute Basis für spätere Szenariovergleiche.

- **Empfohlene Pflegekonventionen:**
  - KPI-Definitionen (Wartezeit je Station, Gesamtprozesszeit) explizit dokumentieren und bei Änderungen versionieren.
  - Bei neuen Stationen (z. B. Labor) dieselbe Struktur beibehalten: eigene `*Phase.cs`, Konfiguration, Diagramm.

- **Technische Notiz:**
  - Für reproduzierbare Ergebnisse sollten `RANDOM_SEED` und alle Konfigurationswerte in Ergebnisreports mit ausgegeben werden.

## 4) Ablauf (End-to-End)

1. `Program.cs` initialisiert Datencontainer und Prozesssteuerung.
2. `PatientenProzess.FuehreAus()` simuliert 5 Arbeitstage.
3. Pro Tag werden Ressourcen erstellt (`Resource` aus SimSharp).
4. `PatientenGenerator` erzeugt Ankünfte und startet Patient-Prozesse.
5. Jeder Patient durchläuft je nach Pfad:
   - Rezeption
   - ggf. Wartezimmer / Schwester
   - Arzt
6. Jede Phase schreibt Events in den Trace und Zeiten in `SimulationsDaten`.
7. Nach Simulationsende:
   - Diagramme werden erzeugt
   - Trace-Datei wird exportiert
   - Kennzahlen werden in der Konsole ausgegeben

## 5) Datenflüsse

- **Input/Parameter:**
  - Konfigurationen in `Ressourcen/*Konfiguration.cs`
  - Globale Simulationsparameter in `SimulationKonfiguration.cs`
- **Laufzeitdaten:**
  - Speicherung in `SimulationsDaten`
- **Output-Artefakte:**
  - `klinik_trace.txt` (Ereignisprotokoll)
  - `images/*.png` (Diagramme)

## 6) Externe Abhängigkeiten

Aus `simSharpSimulation.csproj`:

- `SimSharp` – diskrete Ereignissimulation
- `MathNet.Numerics` – Verteilungen und Zufallsziehungen
- `ScottPlot` – Diagrammerzeugung

## 7) Erweiterungspunkte

Typische Erweiterungen ohne großen Umbau:

- Neue Station (z. B. Labor) als eigene Phase
- Zusätzliche KPIs in `SimulationsDaten`
- Alternative Verteilungen für Ankünfte/Servicezeiten
- Szenarienvergleich über unterschiedliche Konfigurationssets
- Export zusätzlicher Reports (CSV/JSON)

## 8) Hinweise zur Pflege

- Fachlogik pro Station in den jeweiligen `*Phase.cs` Dateien halten.
- Konstante Werte nur in den Konfigurationsklassen ändern.
- Neue Messgrößen immer zentral in `SimulationsDaten` aufnehmen.
- Diagrammlogik ausschließlich in `GenerateDiagramms.cs` erweitern.

---
