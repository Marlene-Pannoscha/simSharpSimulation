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
            Func<Simulation, int, bool, Resource, BeweglicherSchwesterPool, BeweglicherArztPool, IEnumerable<Event>> patientFactory)
        {
            List<Ankunft> ankunftszeiten = ErzeugeAnkunftszeiten(rnd)
                .OrderBy(a => a.Zeit)
                .ThenBy(a => a.DrawIndex)
                .ToList();

            List<Ankunft> warteschlangeVorOeffnung = ankunftszeiten
                .Where(a => a.Zeit < 0.0)
                .ToList();
            List<Ankunft> ankuenfteAbOeffnung = ankunftszeiten
                .Where(a => a.Zeit >= 0.0)
                .ToList();

            int patientCount = patientIdStart;
            foreach (Ankunft ankunft in warteschlangeVorOeffnung)
            {
                daten.LogEvent(ankunft.Zeit, "wartet_vor_oeffnung", patientCount);
                env.Process(patientFactory(env, patientCount, ankunft.HatTermin, rezeption, schwestern, aerzte));
                patientCount++;
            }

            foreach (Ankunft ankunft in ankuenfteAbOeffnung)
            {
                double warteBisAnkunft = ankunft.Zeit - (env.Now - env.StartDate).TotalMinutes;
                if (warteBisAnkunft > 0.0)
                {
                    yield return env.Timeout(TimeSpan.FromMinutes(warteBisAnkunft));
                }

                env.Process(patientFactory(env, patientCount, ankunft.HatTermin, rezeption, schwestern, aerzte));
                patientCount++;
            }
        }

        private static List<Ankunft> ErzeugeAnkunftszeiten(Random rnd)
        {
            var ankunftszeiten = new List<Ankunft>();
            int drawIndex = 0;
            int terminPatienten = (int)Math.Round(
                PatientenKonfiguration.ANZAHL_PATIENTEN_TAG *
                PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT);

            for (int i = 0; i < terminPatienten; i++)
            {
                double zeit = MathNet.Numerics.Distributions.Normal.Sample(
                    rnd,
                    PatientenKonfiguration.ERWARTUNGSWERT,
                    PatientenKonfiguration.STANDARDABWEICHUNG);

                if (zeit <= SimulationKonfiguration.SIMULATIONSDAUER)
                {
                    ankunftszeiten.Add(new Ankunft(zeit, true, drawIndex));
                }

                drawIndex++;
            }

            double mittlereZwischenankunftszeit = Math.Max(
                0.0001,
                PatientenKonfiguration.OHNE_TERMIN_MITTLERE_ZWISCHENANKUNFTSZEIT_MINUTEN);
            double rateProMinute = 1.0 / mittlereZwischenankunftszeit;
            if (rateProMinute > 0.0)
            {
                double zeit = 0.0;
                while (true)
                {
                    zeit += MathNet.Numerics.Distributions.Exponential.Sample(rnd, rateProMinute);
                    if (zeit > SimulationKonfiguration.SIMULATIONSDAUER)
                    {
                        break;
                    }

                    ankunftszeiten.Add(new Ankunft(zeit, false, drawIndex));
                    drawIndex++;
                }
            }

            return ankunftszeiten;
        }
        private readonly record struct Ankunft(double Zeit, bool HatTermin, int DrawIndex);
    }
}
