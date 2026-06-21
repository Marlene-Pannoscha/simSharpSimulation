using System;
using System.Collections.Generic;
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

            // Exponentielle Zwischenankunftszeiten je Tagesphase ergeben einen
            // inhomogenen Poisson-Prozess mit konstanter Rate pro Phase.
            var ankunftszeiten = new List<(double zeit, int drawIndex)>();
            int drawIndex = 0;
            GenerierePhase(ankunftszeiten, rnd, 0.0, 120.0,
                PatientenKonfiguration.ZWISCHENANKUNFT_ERSTE_2_STUNDEN_MINUTEN, ref drawIndex);
            GenerierePhase(ankunftszeiten, rnd, 120.0, 300.0,
                PatientenKonfiguration.ZWISCHENANKUNFT_NAECHSTE_3_STUNDEN_MINUTEN, ref drawIndex);
            GenerierePhase(ankunftszeiten, rnd, 300.0, SimulationKonfiguration.SIMULATIONSDAUER,
                PatientenKonfiguration.ZWISCHENANKUNFT_LETZTE_3_STUNDEN_MINUTEN, ref drawIndex);

            int restAufnahmeplaetze = BerechneAufnahmestoppKapazitaet();
            daten.ErfassePrognoseAufnahmepruefung(env.StartDate, aufnahmeStoppMinuten, restAufnahmeplaetze);

            int patientCount = patientIdStart;
            foreach (var eintrag in ankunftszeiten)
            {
                double ankunftszeit = eintrag.zeit;
                double warteBisAnkunft = ankunftszeit - (env.Now - env.StartDate).TotalMinutes;

                if (warteBisAnkunft > 0)
                    yield return env.Timeout(TimeSpan.FromMinutes(warteBisAnkunft));

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

        private static void GenerierePhase(
            ICollection<(double zeit, int drawIndex)> ankunftszeiten,
            Random rnd,
            double phasenStart,
            double phasenEnde,
            double mittlereZwischenankunftMinuten,
            ref int drawIndex)
        {
            phasenEnde = Math.Min(phasenEnde, SimulationKonfiguration.SIMULATIONSDAUER);
            if (phasenEnde <= phasenStart)
                return;
            if (mittlereZwischenankunftMinuten <= 0.0)
                throw new InvalidOperationException("Die mittlere Zwischenankunftszeit muss groesser als 0 sein.");

            double zeit = phasenStart;
            double rateProMinute = 1.0 / mittlereZwischenankunftMinuten;
            while (true)
            {
                zeit += MathNet.Numerics.Distributions.Exponential.Sample(rnd, rateProMinute);
                if (zeit >= phasenEnde)
                    break;

                ankunftszeiten.Add((zeit, drawIndex++));
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
            double restMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double mittlereArztBehandlungsdauer = Math.Max(0.1, ArztKonfiguration.MITTLERE_BEHANDLUNGSDAUER);
            int kapazitaet = (int)Math.Floor((restMinuten * ArztKonfiguration.ANZAHL_AERZTE) / mittlereArztBehandlungsdauer);
            return Math.Max(0, kapazitaet);
        }
    }
}
