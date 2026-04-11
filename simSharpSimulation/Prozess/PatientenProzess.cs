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

        // Schritt 1: Vorbereitung (Der Konstruktor)
        // Erhält einen Startwert für den Zufallsgenerator und ein Objekt zum Speichern der Ergebnisse.
        public PatientenProzess(int randomSeed, SimulationsDaten daten)
        {
            this.rnd = new Random(randomSeed);
            this.daten = daten;
        }

        // Schritt 2: Der Start
        // Richtet die Simulationsuhr und die Ärzte ein und startet den Ablauf.
        public void FuehreAus()
        {
            // Wir simulieren eine Arbeitswoche: 5 Tage (Montag bis Freitag).
            // Der 3. Januar 2000 war ein Montag.
            DateTime startDatum = new DateTime(2000, 1, 3);

            for (int tag = 0; tag < 5; tag++) // 0: Montag, 1: Dienstag, ... 4: Freitag
            {
                // Jeder Tag bekommt seine eigene Simulations-Umgebung (Uhr) und neue Ressourcen.
                // Das Datum wird für jeden Durchlauf um 'tag' Tage erhöht.
                var env = new Simulation(startDatum.AddDays(tag));
                var arzt = new Resource(env, capacity: ArztKonfiguration.ANZAHL_AERZTE);
                var schwester = new Resource(env, capacity: SchwesterKonfiguration.ANZAHL_SCHWESTERN);
                var rezeption = new Resource(env, capacity: RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN);

                // Schritt 3: PatientenGenerator für den jeweiligen Tag starten
                // PatientenGenerator liefert die Ankunftszeiten und startet für jede Ankunft
                // diesen Patient()-Ablauf als eigenen Simulationsprozess.
                env.Process(PatientenGenerator.Generiere(env, rezeption, arzt, schwester, rnd, daten, Patient));
                // Simulation für diesen einen Tag laufen lassen (z.B. 8 Stunden / 480 Minuten)
                env.Run(TimeSpan.FromMinutes(SimulationKonfiguration.SIMULATIONSDAUER));
            }
        }

        /*Schritt 4: Der Weg des Patienten
        Beschreibt exakt, was passiert, von der Tür bis zur Entlassung.
        Prozesslogik eines einzelnen Patienten in der Klinik.
        /// Ablauf: Ankunft -> Schwester -> Arzt -> Abgang
        */
        private IEnumerable<Event> Patient(Simulation env, int patientId, Resource rezeption, Resource schwester, Resource arzt)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // EREIGNIS 1: Patient betritt die Klinik
            daten.LogEvent(nowMinutes, "betritt_klinik", patientId);
            double ankunftszeit = nowMinutes;
            daten.EchteAnkunftszeiten.Add(ankunftszeit);

            // --- REZEPTION (RECEPTION) PHASE ---
            foreach (var ev in DurchlaufeRezeption(env, patientId, rezeption, ankunftszeit))
                yield return ev;

            bool hatTermin = rnd.NextDouble() < PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT;
            bool brauchtVorbereitung = false;
            bool direktZurSchwester = false;
            bool ueberspringeSchwester = false;

            if (hatTermin)
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "hat_termin", patientId);
                brauchtVorbereitung = rnd.NextDouble() < PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT;
                if (brauchtVorbereitung)
                {
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "benoetigt_schwester_vorbereitung", patientId);

                    int users = ErmittleAktiveNutzer(schwester);
                    if (users < SchwesterKonfiguration.ANZAHL_SCHWESTERN)
                    {
                        direktZurSchwester = true;
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_frei", patientId);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_schwester_zimmer", patientId);
                    }
                    else
                    {
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_nicht_frei", patientId);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer", patientId);

                        double wartezimmerDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                            rnd,
                            1.0 / PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER);
                        yield return env.Timeout(TimeSpan.FromMinutes(wartezimmerDauer));

                        nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                        daten.LogEvent(nowMinutes, "verlaesst_wartezimmer", patientId);
                    }
                }
                else
                {
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "keine_schwester_vorbereitung", patientId);
                    ueberspringeSchwester = true;
                }
            }
            else
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "hat_keinen_termin", patientId);
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer", patientId);

                double wartezimmerDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                    rnd,
                    1.0 / PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER);
                yield return env.Timeout(TimeSpan.FromMinutes(wartezimmerDauer));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "verlaesst_wartezimmer", patientId);
            }

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
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "ueberspringt_schwester", patientId);
            }

            // --- ARZT (DOCTOR) PHASE ---
            foreach (var ev in DurchlaufeSchwester(
                env,
                patientId,
                schwester,
                ankunftszeit,
                direktZurSchwester,
                pruefeVorbereitungNachZimmer: false,
                wahrscheinlichkeitVorbereitung: 0.0))
                yield return ev;

            // --- ARZT (DOCTOR) PHASE ---
            foreach (var ev in DurchlaufeArzt(env, patientId, arzt, ankunftszeit))
                yield return ev;

            // EREIGNIS 10: Patient verlässt die Klinik
            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
        }

        private IEnumerable<Event> DurchlaufeRezeption(Simulation env, int patientId, Resource rezeption, double ankunftszeit)
        {
            foreach (var ev in RezeptionPhase.DurchlaufeRezeption(env, patientId, rezeption, ankunftszeit, rnd, daten))
                yield return ev;
        }

        private IEnumerable<Event> DurchlaufeSchwester(
            Simulation env,
            int patientId,
            Resource schwester,
            double ankunftszeit,
            bool direktZurSchwester,
            bool pruefeVorbereitungNachZimmer,
            double wahrscheinlichkeitVorbereitung)
        {
            foreach (var ev in SchwesterPhase.DurchlaufeSchwester(
                env,
                patientId,
                schwester,
                ankunftszeit,
                direktZurSchwester,
                pruefeVorbereitungNachZimmer,
                wahrscheinlichkeitVorbereitung,
                rnd,
                daten))
                yield return ev;
        }

        private IEnumerable<Event> DurchlaufeArzt(Simulation env, int patientId, Resource arzt, double ankunftszeit)
        {
            foreach (var ev in ArztPhase.DurchlaufeArzt(env, patientId, arzt, ankunftszeit, rnd, daten))
                yield return ev;
        }

        private static int ErmittleAktiveNutzer(Resource resource)
        {
            var usersProperty = typeof(Resource).GetProperty("Users", BindingFlags.NonPublic | BindingFlags.Instance);
            var usersCollection = usersProperty?.GetValue(resource) as IReadOnlyCollection<Request>;
            return usersCollection?.Count ?? 0;
        }

    }
}
