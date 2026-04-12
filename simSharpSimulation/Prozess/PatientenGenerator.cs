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
            Resource arzt,
            Resource schwester,
            Random rnd,
            SimulationsDaten daten,
            Func<Simulation, int, Resource, Resource, Resource, IEnumerable<Event>> patientFactory)
        {
            var ankunftszeiten = new List<double>();

            for (int i = 0; i < PatientenKonfiguration.ANZAHL_PATIENTEN_TAG; i++)
            {
                double z = MathNet.Numerics.Distributions.Normal.Sample(
                    rnd,
                    PatientenKonfiguration.ERWARTUNGSWERT,
                    PatientenKonfiguration.STANDARDABWEICHUNG);

                if (z <= SimulationKonfiguration.SIMULATIONSDAUER)
                    ankunftszeiten.Add(z);
            }

            ankunftszeiten.Sort();

            int patientCount = 1;
            foreach (double ankunftszeit in ankunftszeiten)
            {
                double warteBisAnkunft = ankunftszeit - (env.Now - env.StartDate).TotalMinutes;
                if (warteBisAnkunft > 0)
                    yield return env.Timeout(TimeSpan.FromMinutes(warteBisAnkunft));

                env.Process(patientFactory(env, patientCount, rezeption, schwester, arzt));
                patientCount++;
            }
        }
    }
}
