using System;
using System.Collections.Generic;
using System.Reflection;
using SimSharp;

// Ein 'namespace' (Namensraum) ist wie ein Ordner für Klassen, um den Code zu organisieren und Namenskonflikte zu vermeiden.
namespace simSharpSimulation
{
    /* Enthält die komplette Ablauf-Logik der Simulation:
     - Patientenprozess
     - Generator für Ankünfte
     - Start/Run der SimSharp-Umgebung
    */
    /* internal: Klasse ist nur innerhalb dieses Projekts sichtbar.
    sealed: Keine andere Klasse darf von dieser Klasse erben (sie ist "versiegelt").
    class: Der Bauplan für die Klinik-Simulation. */
    internal sealed class PatientenProzess
    {
        private readonly Random rnd;
        private readonly SimulationsDaten daten;

        // Schritt P1: Vorbereitung (Konstruktor)
        // Erhält einen Startwert für den Zufallsgenerator und ein Objekt zum Speichern der Ergebnisse.
        public PatientenProzess(int randomSeed, SimulationsDaten daten)
        {
            this.rnd = new Random(randomSeed);
            this.daten = daten;
        }

        // Schritt P2: Der Start
        // Richtet die Simulationsuhr und die Ärzte ein und startet den Ablauf.
        public void FuehreAus()
        {
            // Phase P-A: Tages-Simulation vorbereiten und starten.
            // Wir simulieren eine Arbeitswoche: 5 Tage (Montag bis Freitag).
            // Der 3. Januar 2000 war ein Montag.
            DateTime startDatum = new DateTime(2000, 1, 3);
            TimeSpan maximaleTagesdauer = BerechneMaximaleTagesdauer();

            for (int tag = 0; tag < 5; tag++) // 0: Montag, 1: Dienstag, ... 4: Freitag
            {
                // Schritt P2.1: Für jeden Tag eine neue Simulationsumgebung erzeugen.
                // Jeder Tag bekommt seine eigene Simulations-Umgebung (Uhr) und neue Ressourcen.
                // Das Datum wird für jeden Durchlauf um 'tag' Tage erhöht.
                var env = new Simulation(startDatum.AddDays(tag));
                var aerzte = new List<PriorityResource>();
                for (int i = 0; i < ArztKonfiguration.ANZAHL_AERZTE; i++)
                {
                    aerzte.Add(new PriorityResource(env, capacity: 1));
                }
                var schwestern = new List<PriorityResource>();
                for (int i = 0; i < SchwesterKonfiguration.ANZAHL_SCHWESTERN; i++)
                {
                    schwestern.Add(new PriorityResource(env, capacity: 1));
                }
                var rezeption = new Resource(env, capacity: RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN);

                // Schritt P3: PatientenGenerator für den jeweiligen Tag starten
                // PatientenGenerator liefert die Ankunftszeiten und startet für jede Ankunft
                // diesen Patient()-Ablauf als eigenen Simulationsprozess.
                // Eindeutige Patienten-IDs pro Tag, damit Trace-Auswertungen (z.B. Zeitachse eines Patienten) sauber sind.
                int patientIdStart = (tag * 10_000) + 1;
                env.Process(PatientenGenerator.Generiere(env, rezeption, aerzte, schwestern, rnd, daten, patientIdStart, Patient));

                // Schritt P2.2: Tages-Simulation ausführen.
                // Die Ankünfte enden nach SIMULATIONSDAUER, aber einzelne Prozesse können
                // noch nachlaufen. Ein fester Nachlaufpuffer verhindert, dass der Tag bei
                // offenen Warteschlangen oder extrem langen Zufallsdauern unbegrenzt läuft.
                env.Run(maximaleTagesdauer);
            }
        }

        private static TimeSpan BerechneMaximaleTagesdauer()
        {
            const double nachlaufPufferMinuten = 180.0;
            return TimeSpan.FromMinutes(SimulationKonfiguration.SIMULATIONSDAUER + nachlaufPufferMinuten);
        }

        /*Schritt P4: Der Weg des Patienten
        Beschreibt exakt, was passiert, von der Tür bis zur Entlassung.
        Prozesslogik eines einzelnen Patienten in der Klinik.
        /// Ablauf: Ankunft -> Rezeption -> (Wartezimmer) -> Schwester -> Arzt -> Abgang
        Hinweis zum Realitätsmodell:
        - Es gibt KEINE harte technische Priorität in den Schwester/Arzt-Queues.
        - Terminpatienten warten im Schnitt kürzer über kürzere Wartezimmerdauer.
        - Patienten ohne Termin warten im Schnitt länger, laufen aber parallel weiter.
        */
        private IEnumerable<Event> Patient(Simulation env, int patientId, Resource rezeption, List<PriorityResource> schwestern, List<PriorityResource> aerzte)
        {
            TimeSpan eingangZurRezeptionDauer = TimeSpan.FromSeconds(SimulationKonfiguration.BEWEGUNGSZEIT_EINGANG_ZUR_REZEPTION_SEKUNDEN);
            TimeSpan interneBewegungsdauer = TimeSpan.FromSeconds(SimulationKonfiguration.BEWEGUNGSZEIT_INNERHALB_KLINIK_SEKUNDEN);
            TimeSpan arztZumAusgangDauer = TimeSpan.FromSeconds(SimulationKonfiguration.BEWEGUNGSZEIT_ARZT_ZUM_AUSGANG_SEKUNDEN);
            TimeSpan rezeptionZumAusgangDauer = TimeSpan.FromSeconds(SimulationKonfiguration.BEWEGUNGSZEIT_REZEPTION_ZUM_AUSGANG_SEKUNDEN);

            // Hilfsmethode zum Auswählen einer Ressource
            (T res, int id) WaehleRessource<T>(List<T> ressourcen)
            {
                // Wähle zufällig eine Ressource
                int index = rnd.Next(ressourcen.Count);
                return (ressourcen[index], index + 1); // IDs starten bei 1
            }
            // Phase P-B: Individueller Patientenablauf.
            // Schritt P4.1: Aktuelle Simulationszeit in Minuten holen.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // Schritt P4.2: Patient betritt die Klinik (Startpunkt des individuellen Ablaufs).
            // EREIGNIS 1: Patient betritt die Klinik
            daten.LogEvent(nowMinutes, "betritt_klinik", patientId);

            // Schritt P4.3: Ankunftszeit merken (Basis für Wartezeit-Berechnungen).
            double ankunftszeit = nowMinutes;
            daten.EchteAnkunftszeiten.Add(ankunftszeit);

            // Schritt P4.3B: Patienten-Typ zuweisen basierend auf Verteilung.
            PatientenTyp patientenTyp = WaehlePatientenTyp(rnd);
            daten.ErfassePatientenTyp(patientenTyp);

            // Schritt P4.3A: Terminstatus früh festlegen, damit die Rezeption ihn kennt und loggen kann.
            bool hatTermin = rnd.NextDouble() < PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT;

            // Schritt P4.4: Rezeption durchlaufen.
            // --- REZEPTION (RECEPTION) PHASE ---
            daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zur_rezeption", patientId);
            yield return env.Timeout(eingangZurRezeptionDauer);
            double ankunftszeitRezeption = (env.Now - env.StartDate).TotalMinutes;

            foreach (var ev in RezeptionPhase.DurchlaufeRezeption(env, patientId, rezeption, ankunftszeitRezeption, hatTermin, false, rnd, daten))
                yield return ev;

            // Schritt P4.5: Entscheidungsvariablen für den weiteren Ablauf vorbereiten.
            bool brauchtVorbereitung = false;
            bool direktZurSchwester = false;
            bool ueberspringeSchwester = false;

            // Schritt P4.6: Prüfen, ob der Patient einen Termin hat.
            if (hatTermin)
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "hat_termin", patientId);

                // Schritt P4.7A: Bei Termin prüfen, ob Schwester-Vorbereitung nötig ist.
                brauchtVorbereitung = rnd.NextDouble() < PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT;
                if (brauchtVorbereitung)
                {
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "benoetigt_schwester_vorbereitung", patientId);

                    // Schritt P4.8A: Prüfen, ob sofort eine Schwester frei ist.
                    int users = ErmittleAktiveNutzer(schwestern);
                    if (users < SchwesterKonfiguration.ANZAHL_SCHWESTERN)
                    {
                        // Schritt P4.9A: Schwester ist frei -> direkt ins Schwesterzimmer.
                        direktZurSchwester = true;
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_frei", patientId);
                    }
                    else
                    {
                        // Schritt P4.9B: Keine Schwester frei -> zuerst ins Wartezimmer.
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_nicht_frei", patientId);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer", patientId);

                        // Schritt P4.9C: Der Weg ins Wartezimmer ist eine interne Bewegung.
                        yield return env.Timeout(interneBewegungsdauer);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "betritt_wartezimmer", patientId);

                        // Schritt P4.10B: Terminpatienten warten im Schnitt kürzer im Wartezimmer.
                        double wartezimmerDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                            rnd,
                                1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_SCHWESTER * PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER));
                        yield return env.Timeout(TimeSpan.FromMinutes(wartezimmerDauer));

                        // Schritt P4.11B: Wartezimmer verlassen.
                        nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                        daten.LogEvent(nowMinutes, "verlaesst_wartezimmer", patientId);
                    }
                }
                else
                {
                    // Schritt P4.8B: Termin vorhanden, aber keine Schwester-Vorbereitung nötig.
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "keine_schwester_vorbereitung", patientId);
                    ueberspringeSchwester = true;
                }
            }
            else
            {
                // Schritt P4.7B: Patient hat keinen Termin.
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "hat_keinen_termin", patientId);

                // Auch ohne Termin prüfen, ob eine Schwester-Vorbereitung anfällt.
                brauchtVorbereitung = rnd.NextDouble() < PatientenKonfiguration.OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT;
                if (brauchtVorbereitung)
                {
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "benoetigt_schwester_vorbereitung", patientId);

                    // Prüfen, ob eine Schwester frei ist.
                    int users = ErmittleAktiveNutzer(schwestern);
                    if (users < SchwesterKonfiguration.ANZAHL_SCHWESTERN)
                    {
                        // Schwester ist frei -> direkt ins Schwesterzimmer.
                        direktZurSchwester = true;
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_frei", patientId);
                    }
                    else
                    {
                        // Keine Schwester frei -> zuerst ins Wartezimmer.
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_nicht_frei", patientId);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer", patientId);

                        // Schritt P4.9D: Auch der Weg ins Wartezimmer ist eine interne Bewegung.
                        yield return env.Timeout(interneBewegungsdauer);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "betritt_wartezimmer", patientId);

                        // Ohne Termin warten Patienten im Schnitt länger im Wartezimmer auf die Schwester.
                        double wartezimmerDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                            rnd,
                            1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_SCHWESTER * PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER));
                        yield return env.Timeout(TimeSpan.FromMinutes(wartezimmerDauer));

                        nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                        daten.LogEvent(nowMinutes, "verlaesst_wartezimmer", patientId);
                    }
                }
                else
                {
                    // Keine Schwester-Vorbereitung nötig -> Schwester wird übersprungen.
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "keine_schwester_vorbereitung", patientId);
                    ueberspringeSchwester = true;
                }
            }

            // Schritt P4.10: Falls Schwester nicht übersprungen wird,
            // Schwester-Phase (Variante mit Prüfung) durchlaufen.
            if (!ueberspringeSchwester)
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zur_schwester", patientId);
                yield return env.Timeout(interneBewegungsdauer);
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "betritt_schwesterzimmer", patientId);

                var (schwesterRes, schwesterId) = WaehleRessource(schwestern);
                // --- SCHWESTER (NURSE) PHASE ---
                foreach (var ev in SchwesterPhase.DurchlaufeSchwester(
                    env,
                    patientId,
                    schwesterRes,
                    schwesterId,
                    patientenTyp,
                    ankunftszeit,
                    hatTermin,
                    direktZurSchwester,
                    pruefeVorbereitungNachZimmer: false,
                    rnd,
                    daten))
                    yield return ev;
            }
            else
            {
                // Schritt P4.10B: Schwester wird in diesem Pfad übersprungen (nur bei Terminpatienten).
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "ueberspringt_schwester", patientId);
            }

            // Schritt P4.11: Wartevorgang für den Arzt.
            // Alle Patienten (mit/ohne Termin, mit/ohne Schwester) kommen hier an, bevor sie zum Arzt gehen.
            daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer_fuer_arzt", patientId);

            // Der Weg ins Arzt-Wartezimmer ist ebenfalls eine interne Bewegung.
            yield return env.Timeout(interneBewegungsdauer);
            daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "betritt_wartezimmer_fuer_arzt", patientId);

            double wartezeitFaktor = hatTermin
                ? PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_ARZT
                : PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_ARZT;
            double wartezimmerDauerArzt = MathNet.Numerics.Distributions.Exponential.Sample(
                rnd, 1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_ARZT * wartezeitFaktor));
            yield return env.Timeout(TimeSpan.FromMinutes(wartezimmerDauerArzt));

            // Schritt P4.12: Arzt-Phase durchlaufen.
            // --- ARZT (DOCTOR) PHASE ---
            var (arztRes, arztId) = WaehleRessource(aerzte);
            foreach (var ev in ArztPhase.DurchlaufeArzt(env, patientId, arztRes, arztId, patientenTyp, ankunftszeit, hatTermin, interneBewegungsdauer, rnd, daten))
                yield return ev;

            // Schritt P4.13: Nach dem Arzt entscheidet sich, ob der Patient noch einmal zur Rezeption muss.
            bool gehtNachArztZurRezeption = rnd.NextDouble() < 0.6;
            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, gehtNachArztZurRezeption ? "geht_nach_arzt_zur_rezeption" : "verlaesst_nach_arzt_ohne_rezeption", patientId);

            if (gehtNachArztZurRezeption)
            {
                yield return env.Timeout(interneBewegungsdauer);
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                foreach (var ev in RezeptionPhase.DurchlaufeRezeption(env, patientId, rezeption, nowMinutes, hatTermin, true, rnd, daten))
                    yield return ev;

                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zum_ausgang", patientId);
                yield return env.Timeout(rezeptionZumAusgangDauer);
            }
            else
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zum_ausgang", patientId);
                yield return env.Timeout(arztZumAusgangDauer);
            }

            // Schritt P4.14: Patient verlässt die Klinik (Ende des Patientenablaufs).
            // EREIGNIS 10: Patient verlässt die Klinik
            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);

            // Gesamtprozesszeit = von Klinik-Eintritt bis Klinik-Austritt.
            double gesamtprozesszeit = nowMinutes - ankunftszeit;
            daten.ErfasseGesamtprozesszeit(gesamtprozesszeit, hatTermin);
        }

        // Phase P-C: Delegation an ausgelagerte Phasenklassen.
        // Schritt P8: Interne Hilfsmethode, um Patienten-Typ zu wählen.
        private static PatientenTyp WaehlePatientenTyp(Random rnd)
        {
            double rand = rnd.NextDouble();
            double cumulative = 0.0;
            foreach (var (typ, wahrsch, _, _, _) in PatientenKonfiguration.TYPEN_VERTEILUNG)
            {
                cumulative += wahrsch;
                if (rand <= cumulative)
                    return typ;
            }
            return PatientenTyp.Mittel; // Fallback
        }

        // Schritt P9: Interne Hilfsmethode, um aktuelle Belegung der Ressource zu prüfen.
        private static int ErmittleAktiveNutzer<T>(List<T> ressourcen)
        {
            return ressourcen.Sum(r => {
                var usersProperty = r?.GetType().GetProperty("Users", BindingFlags.NonPublic | BindingFlags.Instance);
                var usersCollection = usersProperty?.GetValue(r) as IReadOnlyCollection<Request>;
                return usersCollection?.Count ?? 0;
            });
        }

    }
}
