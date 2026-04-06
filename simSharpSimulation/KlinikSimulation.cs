using System;
using System.Collections.Generic;
using SimSharp;

namespace simSharpSimulation
{
    /// <summary>
    /// Enthält die komplette Ablauf-Logik der Simulation:
    /// - Patientenprozess
    /// - Generator für Ankünfte
    /// - Start/Run der SimSharp-Umgebung
    /// </summary>
    internal sealed class KlinikSimulation
    {
        private readonly Random rnd;
        private readonly SimulationsDaten daten;

        public KlinikSimulation(int randomSeed, SimulationsDaten daten)
        {
            this.rnd = new Random(randomSeed);
            this.daten = daten;
        }

        public void FuehreAus()
        {
            // env ist die Simulationsuhr.
            var env = new Simulation(new DateTime(2000, 1, 1));
            var arzt = new Resource(env, capacity: SimulationKonfiguration.ANZAHL_AERZTE);

            env.Process(PatientenGenerator(env, arzt));
            env.Run(TimeSpan.FromMinutes(SimulationKonfiguration.SIMULATIONSDAUER));
        }

        /// <summary>
        /// Prozesslogik eines einzelnen Patienten in der Klinik.
        /// </summary>
        private IEnumerable<Event> Patient(Simulation env, int patientId, Resource arzt)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // EREIGNIS 1: Patient betritt die Klinik
            daten.LogEvent(nowMinutes, "betritt_klinik", patientId);
            double ankunftszeit = nowMinutes;
            daten.EchteAnkunftszeiten.Add(ankunftszeit);

            using (var anfrage = arzt.Request())
            {
                // EREIGNIS 2: Patient geht zur Warteschlange
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zu_warteschlange", patientId);
                yield return anfrage;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double wartezeit = nowMinutes - ankunftszeit;
                daten.Wartezeiten.Add(wartezeit);

                // EREIGNIS 3: Patient verlässt die Warteschlange
                daten.LogEvent(nowMinutes, "verlaesst_warteschlange", patientId);
                // EREIGNIS 4: Patient geht zum Arzt
                daten.LogEvent(nowMinutes, "geht_zu_arzt", patientId);

                double behandlungsdauer = MathNet.Numerics.Distributions.Exponential.Sample(
                    rnd,
                    1.0 / SimulationKonfiguration.MITTLERE_BEHANDLUNGSZEIT);

                yield return env.Timeout(TimeSpan.FromMinutes(behandlungsdauer));
            }

            // EREIGNIS 5: Patient verlässt den Arzt
            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_arzt", patientId);
            // EREIGNIS 6: Patient verlässt die Klinik
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
        }

        /// <summary>
        /// Erzeugt alle Patientenankünfte über den Tag und startet deren Prozesse.
        /// </summary>
        private IEnumerable<Event> PatientenGenerator(Simulation env, Resource arzt)
        {
            var ankunftszeiten = new List<double>();

            // 1) Stichprobe von Ankunftszeiten aus Normalverteilung ziehen.
            for (int i = 0; i < SimulationKonfiguration.ANZAHL_PATIENTEN_TAG; i++)
            {
                double z = MathNet.Numerics.Distributions.Normal.Sample(
                    rnd,
                    SimulationKonfiguration.ERWARTUNGSWERT,
                    SimulationKonfiguration.STANDARDABWEICHUNG);

                // 2) Nur Zeiten innerhalb der Öffnungsdauer [0, SIMULATIONSDAUER] behalten.
                if (z >= 0 && z <= SimulationKonfiguration.SIMULATIONSDAUER)
                    ankunftszeiten.Add(z);
            }

            // 3) Sortieren ist wichtig, damit Ereignisse zeitlich korrekt nacheinander laufen.
            ankunftszeiten.Sort();

            int patientCount = 1;
            foreach (double ankunftszeit in ankunftszeiten)
            {
                // Warte bis zur nächsten geplanten Ankunftszeit (Simulationszeit, nicht Echtzeit).
                double warteBisAnkunft = ankunftszeit - (env.Now - env.StartDate).TotalMinutes;
                if (warteBisAnkunft > 0)
                    yield return env.Timeout(TimeSpan.FromMinutes(warteBisAnkunft));

                env.Process(Patient(env, patientCount, arzt));
                patientCount++;
            }
        }
    }
}
