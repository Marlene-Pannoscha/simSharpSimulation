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

            for (int tag = 0; tag < 5; tag++) // 0: Montag, 1: Dienstag, ... 4: Freitag
            {
                // Schritt P2.1: Für jeden Tag eine neue Simulationsumgebung erzeugen.
                // Jeder Tag bekommt seine eigene Simulations-Umgebung (Uhr) und neue Ressourcen.
                // Das Datum wird für jeden Durchlauf um 'tag' Tage erhöht.
                var env = new Simulation(startDatum.AddDays(tag));
                var arzt = new Resource(env, capacity: ArztKonfiguration.ANZAHL_AERZTE);
                var schwester = new Resource(env, capacity: SchwesterKonfiguration.ANZAHL_SCHWESTERN);
                var rezeption = new Resource(env, capacity: RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN);

                // Schritt P3: PatientenGenerator für den jeweiligen Tag starten
                // PatientenGenerator liefert die Ankunftszeiten und startet für jede Ankunft
                // diesen Patient()-Ablauf als eigenen Simulationsprozess.
                env.Process(PatientenGenerator.Generiere(env, rezeption, arzt, schwester, rnd, daten, Patient));

                // Schritt P2.2: Tages-Simulation bis zur konfigurierten Dauer ausführen.
                // Simulation für diesen einen Tag laufen lassen (z.B. 8 Stunden / 480 Minuten)
                env.Run(TimeSpan.FromMinutes(SimulationKonfiguration.SIMULATIONSDAUER));
            }
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
        private IEnumerable<Event> Patient(Simulation env, int patientId, Resource rezeption, Resource schwester, Resource arzt)
        {
            // Phase P-B: Individueller Patientenablauf.
            // Schritt P4.1: Aktuelle Simulationszeit in Minuten holen.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // Schritt P4.2: Patient betritt die Klinik (Startpunkt des individuellen Ablaufs).
            // EREIGNIS 1: Patient betritt die Klinik
            daten.LogEvent(nowMinutes, "betritt_klinik", patientId);

            // Schritt P4.3: Ankunftszeit merken (Basis für Wartezeit-Berechnungen).
            double ankunftszeit = nowMinutes;
            daten.EchteAnkunftszeiten.Add(ankunftszeit);

            // Schritt P4.3A: Terminstatus früh festlegen, damit die Rezeption ihn kennt und loggen kann.
            bool hatTermin = rnd.NextDouble() < PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT;

            // Schritt P4.4: Rezeption durchlaufen.
            // --- REZEPTION (RECEPTION) PHASE ---
            foreach (var ev in DurchlaufeRezeption(env, patientId, rezeption, ankunftszeit, hatTermin))
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
                    int users = ErmittleAktiveNutzer(schwester);
                    if (users < SchwesterKonfiguration.ANZAHL_SCHWESTERN)
                    {
                        // Schritt P4.9A: Schwester ist frei -> direkt ins Schwesterzimmer.
                        direktZurSchwester = true;
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_frei", patientId);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_schwester_zimmer", patientId);
                    }
                    else
                    {
                        // Schritt P4.9B: Keine Schwester frei -> zuerst ins Wartezimmer.
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_nicht_frei", patientId);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer", patientId);

                        // Schritt P4.10B: Terminpatienten warten im Schnitt kürzer im Wartezimmer.
                        double wartezimmerDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                            rnd,
                            1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER * 0.5));
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
                // Schritt P4.7B: Ohne Termin -> zuerst ins Wartezimmer.
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "hat_keinen_termin", patientId);
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer", patientId);

                // Schritt P4.8C: Ohne Termin warten Patienten im Schnitt länger im Wartezimmer.
                double wartezimmerDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                    rnd,
                    1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER * 1.8));
                yield return env.Timeout(TimeSpan.FromMinutes(wartezimmerDauer));

                // Schritt P4.9C: Wartezimmer verlassen.
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "verlaesst_wartezimmer", patientId);

                // Schritt P4.9D: Ohne Termin darf der Patient trotzdem weiter in den medizinischen Prozess.
                daten.LogEvent(nowMinutes, "ohne_termin_darf_weiter", patientId);
            }

            // Schritt P4.10: Falls Schwester nicht übersprungen wird,
            // Schwester-Phase (Variante mit Prüfung) durchlaufen.
            if (!ueberspringeSchwester)
            {
                // --- SCHWESTER (NURSE) PHASE ---
                foreach (var ev in DurchlaufeSchwester(
                    env,
                    patientId,
                    schwester,
                    ankunftszeit,
                    direktZurSchwester,
                    pruefeVorbereitungNachZimmer: true,
                    wahrscheinlichkeitVorbereitung: PatientenKonfiguration.SCHWESTERZIMMER_VORBEREITUNG_WAHRSCHEINLICHKEIT))
                    yield return ev;
            }
            else
            {
                // Schritt P4.10B: Schwester wird in diesem Pfad übersprungen.
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "ueberspringt_schwester", patientId);
            }

            // Schritt P4.11: Zusätzliche Schwester-Phase ohne Vorbereitungsprüfung.
            // (Dieser Ablauf ist wie im bisherigen Verhalten beibehalten.)
            // --- SCHWESTER (NURSE) PHASE ---
            foreach (var ev in DurchlaufeSchwester(
                env,
                patientId,
                schwester,
                ankunftszeit,
                direktZurSchwester,
                pruefeVorbereitungNachZimmer: false,
                wahrscheinlichkeitVorbereitung: 0.0))
                yield return ev;

            // Schritt P4.12: Arzt-Phase durchlaufen.
            // --- ARZT (DOCTOR) PHASE ---
            foreach (var ev in DurchlaufeArzt(env, patientId, arzt, ankunftszeit))
                yield return ev;

            // Schritt P4.13: Patient verlässt die Klinik (Ende des Patientenablaufs).
            // EREIGNIS 10: Patient verlässt die Klinik
            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
        }

        // Phase P-C: Delegation an ausgelagerte Phasenklassen.
        // Schritt P8: Interne Hilfsmethode, um aktuelle Belegung der Ressource zu prüfen.
        private static int ErmittleAktiveNutzer(Resource resource)
        {
            var usersProperty = typeof(Resource).GetProperty("Users", BindingFlags.NonPublic | BindingFlags.Instance);
            var usersCollection = usersProperty?.GetValue(resource) as IReadOnlyCollection<Request>;
            return usersCollection?.Count ?? 0;
        }

    }
}
