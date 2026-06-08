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

            double aufnahmeStoppMinuten = BerechneAufnahmeStoppZeitpunkt();
            int restAufnahmeplaetze = BerechneAufnahmestoppKapazitaet();
            daten.ErfassePrognoseAufnahmepruefung(env.StartDate, aufnahmeStoppMinuten, restAufnahmeplaetze);

            int patientCount = patientIdStart;
            foreach (var eintrag in warteschlangeVorOeffnung)
            {
                daten.LogEvent(eintrag.zeit, "wartet_vor_oeffnung", patientCount);
                env.Process(patientFactory(env, patientCount, rezeption, schwestern, aerzte));
                patientCount++;
            }

            foreach (var eintrag in ankuenfteAbOeffnung)
            {
                double ankunftszeit = eintrag.zeit;
                double warteBisAnkunft = ankunftszeit - (env.Now - env.StartDate).TotalMinutes;

                if (warteBisAnkunft > 0)
                    yield return env.Timeout(TimeSpan.FromMinutes(warteBisAnkunft));

                double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                if (nowMinutes >= aufnahmeStoppMinuten)
                {
                    if (restAufnahmeplaetze <= 0)
                    {
                        daten.ErfassePrognoseAufnahmeAbgewiesen(env.StartDate, nowMinutes, patientCount);
                        daten.LogEvent(nowMinutes, "abgewiesen_vor_klinik_wegen_aufnahmeprognose", patientCount);
                        patientCount++;
                        continue;
                    }

                    restAufnahmeplaetze--;
                    daten.ErfassePrognoseAufnahmeZugelassen(
                        env.StartDate,
                        nowMinutes,
                        patientCount,
                        restAufnahmeplaetze);
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

        private static int BerechneAufnahmestoppKapazitaet()
        {
            double restMinuten = SimulationKonfiguration.PROGNOSE_PRUEFUNG_VOR_SCHLIESSUNG_MINUTEN;
            double mittlereArztDauer = Math.Max(0.1, ArztKonfiguration.MITTLERE_BEHANDLUNGSDAUER);
            return Math.Max(0, (int)Math.Floor((restMinuten * ArztKonfiguration.ANZAHL_AERZTE) / mittlereArztDauer));
        }
    }
}
