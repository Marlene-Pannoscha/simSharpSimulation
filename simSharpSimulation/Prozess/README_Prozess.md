# Prozesslogik der Klinik-Simulation

Diese Datei dokumentiert den aktuellen Stand der Ablauf-, Ressourcen-, Zufalls- und Prognoselogik im Ordner `simSharpSimulation/Prozess`.

## 1. Überblick

Der Prozessbereich besteht aus folgenden Bausteinen:

- `PatientenGenerator.cs` erzeugt die Patientenankünfte und startet für jede zugelassene Ankunft einen eigenen SimSharp-Prozess.
- `PatientenProzess/PatientenProzess.cs` richtet für jeden Arbeitstag die Simulationsumgebung und alle Ressourcen ein.
- `PatientenProzess/PatientenProzess.PatientenAblauf.cs` beschreibt den vollständigen Weg eines Patienten durch die Klinik.
- `PatientenProzess/PatientenProzess.Prognose.cs` berechnet Restzeitprognosen, verwaltet die Aufnahmeprognose und führt prognosebedingte Abbrüche aus.
- `PatientenProzess/PatientenProzess.Helfer.cs` erzeugt die zufälligen Behandlungs- und Wartezimmerdauern.
- `RezeptionPhase.cs`, `SchwesterPhase.cs` und `ArztPhase.cs` kapseln Ressourcenzuteilung, Warteschlangen, Behandlung und Feierabendabbrüche der jeweiligen Station.
- `BeweglicherArztPool.cs` und `BeweglicherSchwesterPool.cs` verbinden eine priorisierte Personalressource mit konkreten Mitarbeiter-IDs.
- `PrognoseRessourcenStatus.cs` bildet Warteschlangen und aktive Bearbeitungen für die Restzeitprognose nach.
- `BehandlungsPhaseErgebnis.cs` meldet dem Hauptprozess, dass ein Patient die Klinik bereits innerhalb einer Stationsphase verlassen hat.

## 2. Tagessteuerung und Ressourcen

`PatientenProzess.FuehreAus()` simuliert `Program.SimulierteArbeitstage`. Der erste simulierte Tag ist Montag, der 3. Januar 2000. Für jeden Tag werden eine neue `Simulation` und neue Ressourcen erzeugt:

- Rezeption als `Resource` mit `RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN`
- Ärzte als `BeweglicherArztPool` auf Basis einer `PriorityResource`
- Schwestern als `BeweglicherSchwesterPool` auf Basis einer `PriorityResource`
- Arztzimmer als eigene `Resource`
- Schwesterzimmer als eigene `Resource`
- je ein `PrognoseRessourcenStatus` für Rezeption, Schwestern und Ärzte

Die Anzahl der Arzt- und Schwesterzimmer stammt aus der Finanz- beziehungsweise Raumkonfiguration. Vor einer Arzt- oder Schwesterbehandlung müssen sowohl ein Mitarbeiter als auch ein passender Raum verfügbar sein. In der aktuellen Implementierung wird der Raum-Request nach der Verfügbarkeitsprüfung wieder freigegeben; die Personalressource bleibt bis zum Ende der Behandlung belegt.

Die Patienten-IDs beginnen pro Tag bei `(TagIndex * 10.000) + 1`. Dadurch bleiben sie über mehrere simulierte Tage eindeutig.

Neue Ankünfte werden nur innerhalb der konfigurierten `SIMULATIONSDAUER` erzeugt. Bereits gestartete Patientenprozesse dürfen nachlaufen. Die SimSharp-Umgebung endet spätestens nach:

```text
SIMULATIONSDAUER + 180 Minuten Nachlaufpuffer
```

## 3. Ankunftsprozess

### 3.1 Verwendete Verteilung

Die Ankünfte folgen einem phasenweise homogenen Poisson-Prozess. Die Zwischenankunftszeiten sind innerhalb jeder Tagesphase unabhängig exponentialverteilt:

```text
T_j ~ Exp(lambda_j)
lambda_j = 1 / m_j
```

Dabei bezeichnet `m_j` die konfigurierte mittlere Zwischenankunftszeit der Phase. Entsprechend ist die Zahl der Ankünfte in einem Zeitintervall der Länge `t` poissonverteilt:

```text
N_j(t) ~ Poisson(lambda_j * t)
```

Die aktuelle Aufteilung lautet:

1. Minute 0 bis 120: `ZWISCHENANKUNFT_ERSTE_2_STUNDEN_MINUTEN`
2. Minute 120 bis 300: `ZWISCHENANKUNFT_NAECHSTE_3_STUNDEN_MINUTEN`
3. Minute 300 bis zum Simulationsende: `ZWISCHENANKUNFT_LETZTE_3_STUNDEN_MINUTEN`

Für jede Phase beginnt die Generierung am Phasenanfang. Wiederholt wird eine exponentialverteilte Zwischenankunftszeit addiert, bis der nächste Zeitpunkt außerhalb der Phase liegt. Eine feste Patientenzahl pro Tag und eine Normalverteilung der Ankunftszeit werden nicht verwendet.

### 3.2 Aufnahmestopp vor Schließung

Der Generator berechnet den Aufnahmestopp als:

```text
SIMULATIONSDAUER - PROGNOSE_PRUEFUNG_VOR_SCHLIESSUNG_MINUTEN
```

Mit der Standardkonfiguration liegt dieser Zeitpunkt 60 Minuten vor Schließung. Generierte Ankünfte ab diesem Zeitpunkt starten keinen Patientenprozess. Sie werden mit `abgewiesen_vor_klinik_wegen_aufnahmeprognose` protokolliert und als durch die Aufnahmeprognose abgewiesen erfasst.

Der Generator dokumentiert außerdem eine theoretische Arztkapazität. Sie ergibt sich aus Simulationsdauer, Anzahl der Ärzte und mittlerer Arztbehandlungsdauer. Die eigentliche Auswahl bereits aktiver Patienten am Prüfzeitpunkt erfolgt jedoch in `PatientenProzess.Prognose.cs`.

## 4. Zufallsentscheidungen und Verteilungen eines Patienten

Beim Eintritt werden die wesentlichen Zufallswerte eines Patienten einmalig gezogen. Prognose und späterer Prozess verwenden dadurch dieselben konkreten Werte.

### 4.1 Patiententyp

Der Patiententyp wird aus `PatientenKonfiguration.TYPEN_VERTEILUNG` ausgewählt:

- `Kurz`
- `Mittel`
- `Lang`

Jeder Typ besitzt eine Wahrscheinlichkeit sowie eigene Mittelwerte und Variationskoeffizienten für Arzt- und Schwesterbehandlungen.

### 4.2 Termin und Vorbereitung

Der Terminstatus folgt einer Bernoulli-Entscheidung mit `TERMIN_WAHRSCHEINLICHKEIT`. Ob eine Schwester-Vorbereitung erforderlich ist, wird anschließend mit einer terminabhängigen Wahrscheinlichkeit bestimmt:

- mit Termin: `TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT`
- ohne Termin: `OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT`

Terminpatienten erhalten keine eigene Ressourcenpriorität. Der Terminstatus verändert in der aktuellen Logik vor allem die Wahrscheinlichkeit der Schwester-Vorbereitung und die Wartezimmerdauer.

### 4.3 Behandlungsdauern

Folgende Dauern sind lognormalverteilt:

- erste Rezeptionsbehandlung
- optionale zweite Rezeptionsbehandlung
- Schwesterbehandlung, abhängig vom Patiententyp
- Arztbehandlung, abhängig vom Patiententyp

Aus Mittelwert `m` und Variationskoeffizient `v` werden die Parameter der Lognormalverteilung berechnet:

```text
Varianz = (v * m)^2
mu      = ln(m) - 0,5 * ln(1 + Varianz / m^2)
sigma   = sqrt(ln(1 + Varianz / m^2))
```

### 4.4 Wartezimmerdauern

Die vorgeschalteten Wartezimmerdauern für Schwester und Arzt sind exponentialverteilt. Ihr Erwartungswert ist jeweils:

```text
mittlere Wartezimmerdauer * Terminfaktor
```

Für Patienten mit und ohne Termin gelten unterschiedliche Faktoren. Diese zufällige Wartezimmerdauer ist von der anschließenden ressourcenbedingten Queue-Wartezeit zu unterscheiden: Nach Ablauf der Wartezimmerdauer kann weiterhin auf Personal oder Raum gewartet werden.

### 4.5 Rückweg zur Rezeption

Nach der Arztbehandlung gehen 60 Prozent der Patienten noch einmal zur Rezeption. Diese Entscheidung wird ebenfalls bereits beim Eintritt gezogen. Die zweite Rezeption bildet beispielsweise Folgetermin- oder Rezeptvorgänge ab.

## 5. Vollständiger Patientenfluss

Der normale End-to-End-Ablauf lautet:

1. Eintritt in die Klinik und Erfassung der Ankunftszeit
2. Zuweisung von Patiententyp, Terminstatus, Behandlungspfad und sämtlichen Zufallsdauern
3. Checkpoint `Ankunft`
4. Bewegung zur Rezeption
5. erste Rezeptionsphase
6. Entscheidung über eine Schwester-Vorbereitung
7. Checkpoint `NachRezeption`
8. optional Wartezimmer und Checkpoint `VorSchwester`
9. optional Schwesterphase und Checkpoint `NachSchwester`
10. Arztwartezimmer und Checkpoint `VorArzt`
11. Arztphase und Checkpoint `NachArzt`
12. optional zweite Rezeptionsphase
13. Bewegung zum Ausgang
14. Verlassen der Klinik und Abschluss aller Prognosen

Wenn keine Schwester-Vorbereitung benötigt wird, wird `ueberspringt_schwester` protokolliert. Benötigt der Patient eine Vorbereitung und ist sofort eine Schwester verfügbar, kann er ohne vorgeschalteten Wartezimmerpfad direkt die Schwesterphase anfordern.

Die Gesamtprozesszeit wird nur beim regulären Abschluss als Zeit zwischen `betritt_klinik` und `verlaesst_klinik` erfasst.

## 6. Rezeption

`RezeptionPhase.DurchlaufeRezeption()` führt folgende Schritte aus:

1. Eintritt in die Rezeptionswarteschlange
2. Registrierung im Prognose-Ressourcenstatus
3. Protokollierung, ob die Rezeption beim Eintritt frei war
4. Warten auf einen Rezeptionsplatz oder auf das Schichtende
5. Erfassung der Queue-Wartezeit
6. Durchführung der zuvor gezogenen lognormalverteilten Rezeptionsdauer
7. Abschluss und Freigabe der Ressource

Bei der ersten Rezeption wird Termin beziehungsweise kein Termin protokolliert. Bei der zweiten Rezeption wird `behandlung_bereits_fertig` sowie `macht_folgetermin_aus_oder_rezept` protokolliert.

Erhält ein wartender Patient bis zum Schichtende keinen Platz, wird der Vorgang mit `bricht_ab_wegen_feierabend_rezeption` beendet. Der Patient geht zum Ausgang, verlässt die Klinik und alle offenen Prognosen werden geschlossen. Eine bereits begonnene Rezeptionsbehandlung darf über das Schichtende hinaus abgeschlossen werden.

## 7. Schwesterphase

Die Schwesterphase verwendet sowohl den beweglichen Schwesterpool als auch die Schwesterzimmer-Ressource.

Die Prioritäten lauten:

```text
Kurz   -> Priorität 1
Mittel -> Priorität 2
Lang   -> Priorität 3
```

Ein kleinerer Zahlenwert besitzt die höhere Priorität. Bei gleicher Priorität verwaltet die `PriorityResource` die Reihenfolge. Der Request wird sofort gestellt, damit wartende Patienten korrekt eingeordnet werden.

Nach erfolgreicher Zuteilung von Schwester und Raum wird eine konkrete Schwester-ID übernommen. Es folgen Bewegung, Betreten des Schwesterzimmers und die zuvor gezogene lognormalverteilte Behandlung. Danach wird die ID wieder an den Pool zurückgegeben.

Wird bis zum Schichtende keine Schwester beziehungsweise kein Raum zugeteilt, erfolgt `bricht_ab_wegen_feierabend_schwester`. Eine bereits begonnene Behandlung darf nach Schichtende beendet werden.

## 8. Arztphase

Die Arztphase arbeitet analog zur Schwesterphase mit dem beweglichen Arztpool und der Arztzimmer-Ressource. Auch hier gilt die Prioritätsreihenfolge `Kurz`, `Mittel`, `Lang`.

Ein Patient zählt als behandelter `Hit`, sobald Arzt und Raum erfolgreich zugeteilt wurden und der Weg zur Behandlung beginnt. Nach dem Betreten des Arztzimmers wird die zuvor gezogene lognormalverteilte Arztbehandlung ausgeführt.

Wird bis zum Schichtende kein Arzt beziehungsweise kein Raum zugeteilt, erfolgt `bricht_ab_wegen_feierabend_arzt`. Eine bereits begonnene Arztbehandlung läuft dagegen bis zum Ende weiter.

## 9. Bewegliche Personalpools

`BeweglicherArztPool` und `BeweglicherSchwesterPool` kapseln jeweils:

- eine `PriorityResource` mit der konfigurierten Mitarbeiterzahl
- eine FIFO-Liste freier Mitarbeiter-IDs
- aktuelle Anzahl freier und belegter Mitarbeiter
- Länge der internen Request-Warteschlange
- Anfordern und Freigeben der Personalressource

Die Prioritätsressource entscheidet, welcher Patient als Nächstes Personal erhält. Die separate ID-Liste ordnet einer begonnenen Behandlung anschließend eine konkrete Arzt- oder Schwester-ID für den Trace zu.

## 10. Prognosemodell

### 10.1 Checkpoints

Restzeitprognosen werden an folgenden Punkten gespeichert:

- `Ankunft`
- `NachRezeption`
- `VorSchwester`, sofern eine Schwesterphase stattfindet
- `NachSchwester`, sofern eine Schwesterphase stattfindet
- `VorArzt`
- `NachArzt`

Jede Prüfung enthält unter anderem Patient-ID, Phase, Zeitpunkt, prognostizierte Restzeit, prognostizierte restliche Bearbeitungszeit, bereits verbrauchte Bearbeitungszeit, Kalibrierungskorrektur und die Entscheidung, ob ein Abschluss bis zum Schichtende erwartet wird.

### 10.2 Bestandteile der Restzeit

Die Prognose verwendet:

- die bereits gezogenen konkreten Behandlungsdauern
- den bereits gezogenen konkreten Patientenpfad
- Bewegungszeiten
- die gezogenen exponentiellen Wartezimmerdauern
- geschätzte Queue-Wartezeiten an Rezeption, Schwester und Arzt
- Kapazität und aktive Endzeiten der jeweiligen Station
- geplante zukünftige Ankunftszeitpunkte an den Stationen
- Bearbeitungsdauer, Priorität und Einfügereihenfolge wartender Patienten

Damit ist die Prognose nicht mehr ausschließlich mittelwertbasiert. Sie ist queue-sensitiv, bildet die Warteschlangen jedoch in einem separaten Prognosemodell nach und liest nicht den vollständigen internen SimSharp-Zustand aus.

### 10.3 Queue-Schätzung

`PrognoseRessourcenStatus` verwaltet aktive Bearbeitungen und wartende beziehungsweise bereits geplante Patienten. Für eine Schätzung werden die Server nach ihrem frühesten Frei-Zeitpunkt betrachtet. Der jeweils nächste Patient wird nach folgenden Regeln gewählt:

1. Bereits am Serverzeitpunkt verfügbare Patienten
2. niedrigster Prioritätswert
3. frühere Einfügereihenfolge

Ist noch niemand verfügbar, wird zur nächsten geplanten Bereit-Zeit vorgerückt. Die geschätzte Queue-Wartezeit ist die Differenz zwischen dem berechneten Bearbeitungsbeginn und der Stationsankunft des betrachteten Patienten.

Die Queue-Prognose berücksichtigt Personal- beziehungsweise Rezeptionskapazitäten. Die gesonderten Arzt- und Schwesterzimmerkapazitäten werden im Prognose-Ressourcenstatus derzeit nicht abgebildet.

### 10.4 Kalibrierung

Vor der Schichtendeprüfung kann `SimulationsDaten` eine phasenspezifische Restzeitkorrektur liefern. Die Kalibrierung ist standardmäßig aktiv und beginnt nach mindestens zwölf abgeschlossenen Beobachtungen einer Phase. Sie verwendet eine geglättete Abweichung mit Lernrate 0,04 und begrenzt die Korrektur auf plus oder minus zwölf Minuten.

Die Entscheidung am Checkpoint lautet:

```text
aktueller Zeitpunkt + kalibrierte Prognose-Restzeit <= SIMULATIONSDAUER
```

Ist die Bedingung falsch, wird der Patientenprozess sofort prognosebedingt beendet.

## 11. Prognosebedingte Abbrüche

Bei einer negativen Restzeitprognose führt `BrichWegenPrognoseAb()` den Patienten über einen zum Standort passenden Weg zum Ausgang. Erfasst werden Phase und Zeitpunkt des Prognoseabbruchs. Im Trace wird bewusst kein separates Event `prognose_abbruch` geschrieben; sichtbar sind dort nur `geht_zum_ausgang` und `verlaesst_klinik`.

Verwendete Ausgangswege:

- `Ankunft`: keine zusätzliche Wegzeit
- `NachRezeption`: Rezeption zum Ausgang
- `VorSchwester` und `NachSchwester`: interne Bewegungszeit
- `VorArzt`: interne Bewegungszeit
- `NachArzt`: Arzt zum Ausgang

Nach dem Verlassen werden die offenen Prognosen geschlossen und der Patient aus allen Prognose-Ressourcenstatus entfernt.

## 12. Aufnahmeprognose eine Stunde vor Schließung

Zusätzlich zum Aufnahmestopp des Generators wird zum konfigurierten Prüfzeitpunkt die verbleibende Arztkapazität geschätzt:

```text
Kapazität = floor(Restminuten * Anzahl Ärzte / mittlere Arztbehandlungsdauer)
```

Aktive Patienten werden nach prognostizierter Restzeit, Prüfzeitpunkt und Patient-ID sortiert. Bis zur geschätzten Kapazität werden sie zugelassen, weitere aktive Patienten als abzuweisen markiert. Diese Markierung wird an mehreren Übergängen des Patientenablaufs geprüft. Ein betroffener Patient erhält das Event `abgewiesen_wegen_aufnahmeprognose` und verlässt die Klinik.

Die zwei Ereignisse sind daher zu unterscheiden:

- `abgewiesen_vor_klinik_wegen_aufnahmeprognose`: Ankunft ab dem generatorseitigen Aufnahmestopp; der Patientenprozess startet nicht.
- `abgewiesen_wegen_aufnahmeprognose`: bereits gestarteter Patient wird durch die kapazitätsbasierte Aufnahmeprognose beendet.

## 13. Feierabendlogik und Hit/Miss

An Rezeption, Schwester und Arzt wartet ein Patient jeweils auf die Ressource oder parallel auf das Schichtende. Tritt das Schichtende zuerst ein, wird der Request abgebrochen und der Patient verlässt die Klinik.

Die aktuelle Hit/Miss-Logik verwendet:

- `Hit`: eine Arztbehandlung wurde begonnen
- `Miss`: Abbruch wegen Feierabend an Rezeption, Schwester oder Arzt

Ältere Methoden für Abbrüche wegen maximaler Wartezeit sind in `SimulationsDaten` noch vorhanden, werden von den aktuellen Stationsphasen aber nicht verwendet. Es existiert derzeit keine separate maximale Wartezeit als Abbruchkriterium.

## 14. Wichtige Trace-Ereignisse

Bewegungen folgen dem Muster:

- `geht_*`: Bewegung beginnt
- `betritt_*`: Ziel wird nach der jeweiligen Bewegungszeit erreicht

Wichtige Gruppen sind:

- Klinik: `betritt_klinik`, `geht_zum_ausgang`, `verlaesst_klinik`
- Rezeption: `betritt_rezeption_warteschlange`, `rezeption_frei`, `rezeption_nicht_frei`, `startet_rezeption`, `beendet_rezeption`
- Schwester: `betritt_wartezimmer_schwester`, `betritt_schwester_warteschlange`, `startet_schwester_prozess`, `beendet_schwester_prozess`
- Arzt: `betritt_wartezimmer_fuer_arzt`, `startet_arzt_behandlung`, `beendet_arzt_behandlung`
- Feierabend: `bricht_ab_wegen_feierabend_rezeption`, `bricht_ab_wegen_feierabend_schwester`, `bricht_ab_wegen_feierabend_arzt`
- Aufnahmeprognose: `abgewiesen_vor_klinik_wegen_aufnahmeprognose`, `abgewiesen_wegen_aufnahmeprognose`

## 15. Rolle von `BehandlungsPhaseErgebnis`

Eine Stationsphase kann einen Patienten bereits vollständig zum Ausgang führen, beispielsweise bei einem Feierabendabbruch. `BehandlungsPhaseErgebnis.PatientHatKlinikVerlassen` verhindert anschließend, dass der übergeordnete Patientenablauf mit der nächsten Station fortgesetzt wird.

## 16. Aktuelle Modellgrenzen

- Der Ankunftsprozess besitzt drei feste Tagesphasen; innerhalb einer Phase ist die Rate konstant.
- Generierte Ankünfte ab dem Aufnahmestopp werden unabhängig von einer eventuell noch vorhandenen Einzelkapazität abgewiesen.
- Die Prognose bildet Personal- und Rezeptionswarteschlangen nach, aber nicht die zusätzlichen Raumkapazitäten.
- Raum-Requests dienen aktuell als Verfügbarkeitsprüfung und werden vor dem eigentlichen Behandlungsende freigegeben.
- Die Prognose kennt die vorab gezogenen Zufallsdauern und Pfadentscheidungen. Sie ist dadurch eine simulationsinterne Prognose und keine Vorhersage ausschließlich aus Informationen, die in einer realen Klinik zu diesem Zeitpunkt sicher bekannt wären.
- Prognoseabbrüche besitzen kein eigenes Trace-Event.
- Eine begonnene Behandlung darf das Schichtende überschreiten; nur wartende Patienten brechen wegen Feierabend ab.
- Die Simulation wird durch den festen Nachlaufpuffer von 180 Minuten begrenzt.

## 17. Pflegehinweise

- Änderungen am Ankunftsmodell müssen in `PatientenGenerator.cs` und in Abschnitt 3 dokumentiert werden.
- Neue Prozessstationen sollten eine eigene Phasenklasse und einen eigenen Prognose-Ressourcenstatus erhalten.
- Änderungen an Prioritäten müssen sowohl in den Stationsphasen als auch in `PatientenProzess.Prognose.cs` nachvollzogen werden.
- Neue Abbruchgründe müssen in `SimulationsDaten`, Trace-Zustandsmapping, Reports und dieser Datei ergänzt werden.
- Werden Raumressourcen künftig über die gesamte Behandlung gehalten, muss dies auch im Prognosemodell berücksichtigt werden.
- Prozesslogik, Datensammlung, Diagrammerzeugung und UI-Auswertung sollten weiterhin getrennt bleiben.
