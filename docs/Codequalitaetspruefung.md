# Codequalitaetspruefung

Geprueft wurden die 53 bestehenden C#-Quelldateien und beide Projektdateien. Generierte Dateien in `bin` und `obj` sowie Bilddateien gehoeren nicht zum Quellcodeumfang. Zusaetzlich wurden sechs technische Dateien fuer klar getrennte Verantwortlichkeiten angelegt.

Die Pruefung betrachtet Verantwortlichkeiten, Benennung, Wiederholungen, ungenutzten privaten Code, Eingabevalidierung, Ressourcenlebensdauer und Fehlerbehandlung. Bestehende oeffentliche Konfigurationsnamen, JSON-Felder und Trace-Ereignisse bleiben kompatibel. Nicht jede Datei braucht eine Aenderung.

## Ergebnis pro bestehender Datei

| Datei | Ergebnis |
| --- | --- |
| [simSharpSimulation/Diagramm/GenerateDiagram.cs](../simSharpSimulation/Diagramm/GenerateDiagram.cs) | Projektpfadsuche zentralisiert. |
| [simSharpSimulation/Diagramm/ankuenfte_simulation_vs_theorie.cs](../simSharpSimulation/Diagramm/ankuenfte_simulation_vs_theorie.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/ankunftsverteilung_theorie_pdf_cdf.cs](../simSharpSimulation/Diagramm/ankunftsverteilung_theorie_pdf_cdf.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/arzt_behandlungszeiten_pdf_cdf.cs](../simSharpSimulation/Diagramm/arzt_behandlungszeiten_pdf_cdf.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/auslastung_verteilung.cs](../simSharpSimulation/Diagramm/auslastung_verteilung.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/boxplot_basis_vs_mehr_ressourcen.cs](../simSharpSimulation/Diagramm/boxplot_basis_vs_mehr_ressourcen.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/boxplot_basis_vs_weniger_ressourcen.cs](../simSharpSimulation/Diagramm/boxplot_basis_vs_weniger_ressourcen.cs) | Darstellung nach BoxplotDarstellung.cs ausgelagert; Szenarioberechnung beibehalten. |
| [simSharpSimulation/Diagramm/gesamtprozesszeit_histogramm.cs](../simSharpSimulation/Diagramm/gesamtprozesszeit_histogramm.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/hit_miss_pro_tag.cs](../simSharpSimulation/Diagramm/hit_miss_pro_tag.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/miss_patienten_zeitachse.cs](../simSharpSimulation/Diagramm/miss_patienten_zeitachse.cs) | Gemeinsame Trace-Einleselogik verwendet. |
| [simSharpSimulation/Diagramm/patienten_vergleich_zeitachse.cs](../simSharpSimulation/Diagramm/patienten_vergleich_zeitachse.cs) | Gemeinsame Trace-Einleselogik verwendet. |
| [simSharpSimulation/Diagramm/patienten_zeitachse.cs](../simSharpSimulation/Diagramm/patienten_zeitachse.cs) | Gemeinsame Trace-Einleselogik verwendet. |
| [simSharpSimulation/Diagramm/rezeption_behandlungszeiten_pdf_cdf.cs](../simSharpSimulation/Diagramm/rezeption_behandlungszeiten_pdf_cdf.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/schwester_behandlungszeiten_pdf_cdf.cs](../simSharpSimulation/Diagramm/schwester_behandlungszeiten_pdf_cdf.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/wartezeiten_histogramm.cs](../simSharpSimulation/Diagramm/wartezeiten_histogramm.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/wartezeiten_schwester_histogramm.cs](../simSharpSimulation/Diagramm/wartezeiten_schwester_histogramm.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/wartezeiten_theorie_exponential_mit_ohne_termin.cs](../simSharpSimulation/Diagramm/wartezeiten_theorie_exponential_mit_ohne_termin.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/wartezeiten_vergleich_arzt_schwester.cs](../simSharpSimulation/Diagramm/wartezeiten_vergleich_arzt_schwester.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/wartezeiten_vergleich_mit_ohne_termin.cs](../simSharpSimulation/Diagramm/wartezeiten_vergleich_mit_ohne_termin.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Diagramm/zwischenankunftszeiten_exponential_pdf_cdf.cs](../simSharpSimulation/Diagramm/zwischenankunftszeiten_exponential_pdf_cdf.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Program.cs](../simSharpSimulation/Program.cs) | Diagrammerzeugung und Exporte getrennt; ungenutzte Hilfsmethoden entfernt. |
| [simSharpSimulation/Prozess/ArztPhase.cs](../simSharpSimulation/Prozess/ArztPhase.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Prozess/BehandlungsPhaseErgebnis.cs](../simSharpSimulation/Prozess/BehandlungsPhaseErgebnis.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Prozess/BeweglicherArztPool.cs](../simSharpSimulation/Prozess/BeweglicherArztPool.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Prozess/BeweglicherSchwesterPool.cs](../simSharpSimulation/Prozess/BeweglicherSchwesterPool.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Prozess/MitarbeiterPool.cs](../simSharpSimulation/Prozess/MitarbeiterPool.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Prozess/PatientenGenerator.cs](../simSharpSimulation/Prozess/PatientenGenerator.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Prozess/PatientenProzess/PatientenProzess.Helfer.cs](../simSharpSimulation/Prozess/PatientenProzess/PatientenProzess.Helfer.cs) | Vorhandenen Typkonfigurationszugriff wiederverwendet. |
| [simSharpSimulation/Prozess/PatientenProzess/PatientenProzess.PatientenAblauf.cs](../simSharpSimulation/Prozess/PatientenProzess/PatientenProzess.PatientenAblauf.cs) | Aufruf der Restzeitschaetzung ohne ungenutzten Parameter angepasst. |
| [simSharpSimulation/Prozess/PatientenProzess/PatientenProzess.Prognose.cs](../simSharpSimulation/Prozess/PatientenProzess/PatientenProzess.Prognose.cs) | Vier nicht aufgerufene Schaetzmethoden und einen ungenutzten Parameter entfernt. |
| [simSharpSimulation/Prozess/PatientenProzess/PatientenProzess.cs](../simSharpSimulation/Prozess/PatientenProzess/PatientenProzess.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Prozess/PrognoseRessourcenStatus.cs](../simSharpSimulation/Prozess/PrognoseRessourcenStatus.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Prozess/RezeptionPhase.cs](../simSharpSimulation/Prozess/RezeptionPhase.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Prozess/SchwesterPhase.cs](../simSharpSimulation/Prozess/SchwesterPhase.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Prozess/SimulationsHelfer.cs](../simSharpSimulation/Prozess/SimulationsHelfer.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Ressourcen/ArztKonfiguration.cs](../simSharpSimulation/Ressourcen/ArztKonfiguration.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Ressourcen/KonfigurationJsonExport.cs](../simSharpSimulation/Ressourcen/KonfigurationJsonExport.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Ressourcen/PatientenKonfiguration.cs](../simSharpSimulation/Ressourcen/PatientenKonfiguration.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Ressourcen/Personen.cs](../simSharpSimulation/Ressourcen/Personen.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Ressourcen/RezeptionKonfiguration.cs](../simSharpSimulation/Ressourcen/RezeptionKonfiguration.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/Ressourcen/SchwesterKonfiguration.cs](../simSharpSimulation/Ressourcen/SchwesterKonfiguration.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/SimulationKonfiguration.cs](../simSharpSimulation/SimulationKonfiguration.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/SimulationsDaten.cs](../simSharpSimulation/SimulationsDaten.cs) | Gemeinsame Pfadsuche verwendet; lokale Namen fuer Trace und Ist-Restzeit verdeutlicht. |
| [simSharpSimulation/SimulationsTraceAuswertung.cs](../simSharpSimulation/SimulationsTraceAuswertung.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/WPF Fenster/FinanzWpfFenster.HitMiss.cs](../simSharpSimulation/WPF%20Fenster/FinanzWpfFenster.HitMiss.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/WPF Fenster/FinanzWpfFenster.SimulationsUebersicht.cs](../simSharpSimulation/WPF%20Fenster/FinanzWpfFenster.SimulationsUebersicht.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/WPF Fenster/FinanzWpfFenster.Wartezeiten.cs](../simSharpSimulation/WPF%20Fenster/FinanzWpfFenster.Wartezeiten.cs) | WPF-Darstellung und Auswertungsmodell getrennt. |
| [simSharpSimulation/WPF Fenster/FinanzWpfFenster.cs](../simSharpSimulation/WPF%20Fenster/FinanzWpfFenster.cs) | Ungenutzte Felder entfernt; NaN/Infinity in Bereichspruefung abgewiesen; UI-Fehler protokolliert. |
| [simSharpSimulation/WPF Fenster/Kosten/FinanzRechner.cs](../simSharpSimulation/WPF%20Fenster/Kosten/FinanzRechner.cs) | Ergebniszusammenstellung wiederverwendet; interne Mietkostenhilfe einheitlich benannt. |
| [simSharpSimulation/WPF Fenster/Kosten/FinanzVisualisierung.cs](../simSharpSimulation/WPF%20Fenster/Kosten/FinanzVisualisierung.cs) | Finanzsimulation und Ergebnisdaten ausgelagert; Pfadsuche und Methodennamen vereinheitlicht. |
| [simSharpSimulation/WPF Fenster/Kosten/FinanzWpfFenster.Finanzen.cs](../simSharpSimulation/WPF%20Fenster/Kosten/FinanzWpfFenster.Finanzen.cs) | Nicht aufgerufene zweite Berichtserzeugung entfernt; aktiver gemeinsamer Bericht bleibt bestehen. |
| [simSharpSimulation/WPF Fenster/Prognose/FinanzWpfFenster.Prognose.cs](../simSharpSimulation/WPF%20Fenster/Prognose/FinanzWpfFenster.Prognose.cs) | Geprueft; fuer diese Bereinigung keine Aenderung erforderlich. |
| [simSharpSimulation/WPF Fenster/Prognose/PrognoseVisualisierung.cs](../simSharpSimulation/WPF%20Fenster/Prognose/PrognoseVisualisierung.cs) | Projektpfadsuche zentralisiert. |

## Neue Bausteine

| Datei | Verantwortung |
| --- | --- |
| [simSharpSimulation/ProjektPfade.cs](../simSharpSimulation/ProjektPfade.cs) | Gemeinsame Projektordnersuche mit unveraenderter Suchreihenfolge. |
| [simSharpSimulation/Diagramm/TraceHelfer.cs](../simSharpSimulation/Diagramm/TraceHelfer.cs) | Gemeinsamer Parser fuer die drei Patientenzeitachsen. |
| [simSharpSimulation/Diagramm/BoxplotDarstellung.cs](../simSharpSimulation/Diagramm/BoxplotDarstellung.cs) | Ausgelagerte Boxplot-Darstellung; Windows-Renderingbeschraenkung dokumentiert. |
| [simSharpSimulation/WPF Fenster/FinanzWpfFenster.WartezeitenAuswertung.cs](../simSharpSimulation/WPF%20Fenster/FinanzWpfFenster.WartezeitenAuswertung.cs) | Ausgelagertes Auswertungsmodell; bestehende private Typen und Datenbindungen erhalten. |
| [simSharpSimulation/WPF Fenster/Kosten/FinanzVisualisierung.Simulation.cs](../simSharpSimulation/WPF%20Fenster/Kosten/FinanzVisualisierung.Simulation.cs) | Finanzsimulation, Zeitraumzuordnung und Aggregation getrennt vom Rendering. |
| [simSharpSimulation/WPF Fenster/Kosten/FinanzErgebnis.cs](../simSharpSimulation/WPF%20Fenster/Kosten/FinanzErgebnis.cs) | Finanz-Ergebnisdaten und Projektionen getrennt von Simulation und Rendering. |

## Validierung

- `dotnet build simSharpSimulation.csproj --no-restore --verbosity quiet`: keine Warnungen oder Fehler.
- `dotnet build simSharpSimulation/simSharpSimulation.csproj --no-restore --verbosity quiet`: keine Warnungen oder Fehler.
- Vorher-/Nachher-Vergleich mit Seeds 1, 42 und 2026: Simulationsdaten einschliesslich Trace, Prognoseexporte und WPF-Wartezeit-Auswertungen bytegenau identisch.
- Finanzsimulation fuer Jahr, Winter, Fruehling, Sommer und Herbst: Ergebnisdaten bytegenau identisch. Insgesamt wurden 14 Vergleichsdateien geprueft.
- Zehn Eingabefaelle fuer die WPF-Bereichspruefung: Dezimalpunkt, Dezimalkomma, Grenzen, Bereichsverletzungen, Text, NaN und positive/negative Unendlichkeit geprueft.
- Gemeinsamer Trace-Parser mit Kopfzeilen, fehlerhaften Zeilen und gueltigen Ereignissen geprueft.
- Pfadsuche und neun Diagrammexporte (Patientenzeitachse, Finanzdiagramme, Prognosediagramme und Boxplot) erfolgreich im temporaeren Verzeichnis ausgefuehrt.
- `git diff --check` ohne Befund.

Die Vergleichs- und Exportpruefungen liefen mit einem temporaeren Testprogramm ausserhalb des Repositorys. Das WPF-Fenster wurde nicht interaktiv durchgeklickt. Die Pruefung ist keine Garantie fuer Fehlerfreiheit unter jeder Konfiguration.

## Bewusst erhaltene Modellentscheidungen

- Raum- und Mitarbeiteranforderungen behalten ihre bisherige Freigabereihenfolge. Eine Aenderung ihrer Lebensdauer wuerde das Prozessmodell veraendern.
- Szenariolaeufe nutzen weiterhin globale Konfigurationswerte und setzen sie in `finally` zurueck; sie sind nicht fuer parallele Ausfuehrung vorgesehen.
- Die Queue-Laenge des Mitarbeiterpools wird weiterhin ueber den gekapselten Reflection-Zugriff auf SimSharp ermittelt.
- Die Finanzsimulation ist weiterhin ein eigenes Kapazitaetsmodell und kein direkter Export der ereignisbasierten Patientensimulation.
