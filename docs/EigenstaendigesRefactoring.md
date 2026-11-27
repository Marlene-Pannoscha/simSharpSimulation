# Eigenstaendiges SimSharp-Refactoring

## Ausgangslage und Reihenfolge

Der Ausgangsstand enthielt bereits die Trennung zwischen Tagessteuerung,
Behandlungsplan, Aufnahmeprognose, Tagesressourcen, Ereignisformat und
Prognosestatistik. Das eigenstaendige Projekt nutzt diese Komponenten weiter.

1. Zuerst vier Referenzszenarien im Projekt verankern. Sie pruefen genaue
   Ereigniszeiten und Reihenfolge sowie die Daten, die aus dem Ablauf entstehen.
2. Konfiguration vor ihrer Uebernahme vollstaendig lesen und pruefen. Ein
   Fehler in der letzten Datei konnte zuvor bereits uebernommene Werte stehen
   lassen. Fehler nennen jetzt Datei und Feld. Negative Lauflaengen werden
   abgewiesen, null Tage bleiben ein leerer Lauf.
3. Ankunftsphasen im Modell zentral benennen und reine Restzeitformeln vom
   SimSharp-Iterator trennen. Die Grenzen bei Minute 120 und 300 sind Teil
   des bestehenden Schemas der Zwischenankunftszeiten.
4. Den identischen Ausgangspfad der Stationsabbrueche und Prognoseabbrueche
   zusammenfuehren. Stationen setzen weiter ihre jeweiligen Zaehler und
   Abbruchereignisse. Der gemeinsame Pfad fuegt denselben Timeout ein.
5. Dateiexport aus dem Konsolenstart herausziehen. Der Trace-Export schreibt
   Kopfzeile und Daten ohne zusaetzliche Listenkopie. Der Konsolenlauf
   befuellte ausserdem eine Queue, die dort nie gelesen wurde. Die oeffentliche
   Queue-Klasse bleibt fuer externe Aufrufer verfuegbar.

## Struktur und Dateien dieser Etappe

| Bereich | Aenderung |
| --- | --- |
| Projekte | `PraxisSimulation.Regression/` mit `Program.cs`, `KonfigurationsRegression.cs` und eigener Projektdatei neu. `simSharpSimulation.slnx` und `PraxisSimulation.Core.csproj` binden den Prueflauf ein. |
| Ressourcen | `KonfigurationJsonExport.cs` liest und uebernimmt Konfiguration. `Konfigurationspruefung.cs` prueft Eingaben. Die JSON-Modelle wurden aus dem Lader nach `SimulationsKonfigurationJson.cs` und `FinanzKonfigurationJson.cs` verschoben. `PatientenKonfiguration.cs` enthaelt die Phasengrenzen. |
| Prozess | `PatientenGenerator.cs` nutzt die zentralen Phasengrenzen. `RestzeitPrognose.cs` enthaelt die zustandsfreien Formeln. `PatientenTagesablauf.PatientenAblauf.cs` sowie die Teile `Prognose` und `Helfer` nutzen die extrahierten Funktionen. `PatientenProzess.cs` prueft Laufparameter. |
| Stationsabbruch | `ArztPhase.cs`, `SchwesterPhase.cs` und `RezeptionPhase.cs` nutzen den Ausgangspfad in `SimulationsHelfer.cs`. Zwei private, in allen Aufrufen leere Mitarbeiter-ID-Parameter entfielen. |
| Export | `Auswertung/SimulationsExport.cs` schreibt die Desktop-Berichte. `Program.cs` orchestriert den Lauf und ruft den Export auf. |

In dieser Etappe wurde keine Datei entfernt. Fachliche Kennzahlen und
Finanzformeln werden weiterhin vom bestehenden Modell berechnet. Fuer die
Gesamtstruktur siehe [ARCHITEKTUR.md](../ARCHITEKTUR.md).

## Eigenstaendige Pruefung

Aus dem Stamm dieses Repositorys mit .NET 10:

```powershell
dotnet build simSharpSimulation.slnx
dotnet run --project PraxisSimulation.Regression --no-build
dotnet run --project simSharpSimulation/simSharpSimulation.csproj -- --nur-simulationszeit
```

Die ersten beiden Befehle bilden den automatisierten Prueflauf. Die
Regressionen benoetigen weder einen gestarteten Server noch eine Desktop-UI.
Der dritte Befehl startet einen eigenstaendigen Konsolenlauf auf Windows.
Fuer einen vollstaendigen Desktop-Lauf mit Exporten das Argument
`--nur-simulationszeit` weglassen. Die Berichte werden dann im Arbeitsordner
erzeugt.

| Szenario | Seed | Tage | Ereignisse | Hit | Miss |
| --- | ---: | ---: | ---: | ---: | ---: |
| Standard | 42 | 1 | 2221 | 71 | 0 |
| Personalengpass | 17 | 3 | 7682 | 209 | 88 |
| Raumengpass | 17 | 3 | 8801 | 297 | 0 |
| Mehr Kapazitaet | 17 | 3 | 8801 | 297 | 0 |

Alle SHA-256-Referenzen stimmten vor und nach der Etappe ueberein. Erfasst
werden Live-Ereignisse, Trace, Warte- und Behandlungsstatistik, taegliche Hit/Miss-
Werte, Prognose-JSON und finanzielle Tagesergebnisse. Die Konfigurationspruefung
deckt fehlende Dateien, fehlerhaftes JSON, ungueltige Ankunftswerte, unveraenderten
Zustand nach Ladefehlern und erneutes Laden historischer JSON-Dateien ab. Die
Laufpruefung deckt null und negative Tage ab.

Im frisch gebauten Desktop-Vergleich ueber 30 Tage mit Seed 42 waren
`klinik_trace.txt`, `prognose_report.txt` und `prognose_daten.json` gegen
die Referenz bytegleich. Beide Laeufe empfingen 2956 Patienten und meldeten
266336,00 EUR Jahresgewinn. Solution und separates Desktop-Projekt bauten
ohne Warnungen und Fehler.

## Fachliche Befunde fuer eine separate Entscheidung

- Arzt und Schwester geben die Raumressource direkt nach der Zuteilung frei.
  Bei Weg und Behandlung bleibt die Person gebunden, der Raum steht bereits
  wieder zur Verfuegung. Falls Raumkapazitaet die gleichzeitige Behandlung
  begrenzen soll, weicht die aktuelle Ressourcenlebensdauer davon ab.
- `ErfassePrognoseAufnahmeAbgewiesen` zaehlt Abweisungen in der Tages-Miss-
  Reihe, waehrend der Gesamtzaehler `AnzahlAbgebrochenMiss` bei dieser
  Operation nicht steigt. Eine Summenpruefung muss die verschiedenen
  Definitionen beruecksichtigen.
- Das Theorie-Diagramm rechnet im Nenner immer mit zwei vollen ersten Phasen.
  Bei einer Oeffnungsdauer unter 300 Minuten weicht das von den im Generator
  tatsaechlich aktiven Phasen ab.

Die Etappe behaelt diese Verhaltensweisen bei.

## Technische Schulden

Die statische Konfiguration ist pro Prozess geteilt. Unabhaengige parallele
Szenarien benoetigen eigene Parametersaetze pro Lauf. Der lange
Patienten-Iterator und das Reflection-Feld fuer die Warteschlangenlaenge
bleiben weitere Kandidaten fuer jeweils separat abgesicherte Etappen.
Die Desktop-Berichtserzeugung fuer Jahresfinanzen enthaelt weiterhin
Modellcode neben der Formatierung und Diagrammerzeugung.
