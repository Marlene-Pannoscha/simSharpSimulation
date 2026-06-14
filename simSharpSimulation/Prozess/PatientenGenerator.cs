using System;
using System.Collections.Generic;
using System.Linq;
using SimSharp;

namespace simSharpSimulation
{
    internal static class PatientenGenerator
    {
        public static IEnumerable<Event> Generiere(
            Simulation env,
            Resource rezeption,
            BeweglicherArztPool aerzte,
            BeweglicherSchwesterPool schwestern,
            Random rnd,
            SimulationsDaten daten,
            int patientIdStart,
            Func<Simulation, int, Resource, BeweglicherSchwesterPool, BeweglicherArztPool, IEnumerable<Event>> patientFactory)
        {
            double aufnahmeStoppMinuten = BerechneAufnahmeStoppZeitpunkt();

            // Hier sammeln wir alle geplanten Ankunftszeitpunkte (in Minuten ab Tagesstart).
            // drawIndex sorgt bei gleichen Zeiten für eine stabile (FIFO-)Reihenfolge.
            var ankunftszeiten = new List<(double zeit, int drawIndex)>();

            for (int i = 0; i < PatientenKonfiguration.ANZAHL_PATIENTEN_TAG; i++)
            {
                double z = MathNet.Numerics.Distributions.Normal.Sample(
                    rnd,
                    PatientenKonfiguration.ERWARTUNGSWERT,
                    PatientenKonfiguration.STANDARDABWEICHUNG);

                if (z <= SimulationKonfiguration.SIMULATIONSDAUER)
                    ankunftszeiten.Add((z, i));
            }

            ankunftszeiten = ankunftszeiten
                .OrderBy(x => x.zeit)
                .ThenBy(x => x.drawIndex)
                .ToList();

            var warteschlangeVorOeffnung = ankunftszeiten
                .Where(x => x.zeit < 0)
                .ToList();

            var ankuenfteAbOeffnung = ankunftszeiten
                .Where(x => x.zeit >= 0)
                .ToList();

            int patientCount = patientIdStart;
            foreach (var eintrag in warteschlangeVorOeffnung)
            {
                daten.LogEvent(eintrag.zeit, "wartet_vor_oeffnung", patientCount);

                // Bei Öffnung werden wartende Patienten nacheinander in FIFO-Reihenfolge gestartet.
                // Der Praxisprozess startet erst bei Oeffnung, nicht zum negativen Ankunftszeitpunkt.
                if ((env.Now - env.StartDate).TotalMinutes < 0.0)
                {
                    yield return env.Timeout(TimeSpan.FromMinutes(0.0 - (env.Now - env.StartDate).TotalMinutes));
                }

                env.Process(patientFactory(env, patientCount, rezeption, schwestern, aerzte));
                patientCount++;
            }

            foreach (var eintrag in ankuenfteAbOeffnung)
            {
                double ankunftszeit = eintrag.zeit;
                double warteBisAnkunft = ankunftszeit - (env.Now - env.StartDate).TotalMinutes;

                if (warteBisAnkunft > 0)
                    yield return env.Timeout(TimeSpan.FromMinutes(warteBisAnkunft));

                // Startet den individuellen Ablauf für genau diesen Patienten.
                if (ankunftszeit >= aufnahmeStoppMinuten)
                {
                    double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                    daten.ErfassePrognoseAufnahmeAbgewiesen(env.StartDate, nowMinutes, patientCount);
                    daten.LogEvent(nowMinutes, "abgewiesen_vor_klinik_wegen_aufnahmeprognose", patientCount);
                    patientCount++;
                    continue;
                }

                env.Process(patientFactory(env, patientCount, rezeption, schwestern, aerzte));
                patientCount++;
            }
        }

        private static double BerechneAufnahmeStoppZeitpunkt()
        {
            return Math.Max(
                0.0,
                SimulationKonfiguration.SIMULATIONSDAUER -
                SimulationKonfiguration.PROGNOSE_PRUEFUNG_VOR_SCHLIESSUNG_MINUTEN);
        }
    }
}
