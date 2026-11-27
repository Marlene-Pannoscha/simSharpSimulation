using System;
using System.Collections.Generic;
using SimSharp;
using static simSharpSimulation.SimulationsHelfer;

namespace simSharpSimulation
{
    internal static class PatientenGenerator
    {
        public static IEnumerable<Event> Generiere(
            Simulation env,
            Random rnd,
            SimulationsDaten daten,
            int erstePatientId,
            Func<int, IEnumerable<Event>> erzeugePatientenProzess)
        {
            double aufnahmestoppMinuten = Aufnahmestopp.BerechneZeitpunkt();

            // Exponentielle Zwischenankunftszeiten je Tagesphase ergeben einen
            // inhomogenen Poisson-Prozess mit konstanter Rate pro Phase.
            // Alle Ankuenfte werden vorab gezogen. Diese Reihenfolge der Zufallsziehungen
            // ist fuer reproduzierbare Laeufe mit demselben Seed relevant.
            var ankunftszeiten = new List<double>();
            GenerierePhase(ankunftszeiten, rnd, 0.0, PatientenKonfiguration.BeginnZweiteAnkunftsphaseMinuten,
                PatientenKonfiguration.ZWISCHENANKUNFT_ERSTE_2_STUNDEN_MINUTEN);
            GenerierePhase(ankunftszeiten, rnd, PatientenKonfiguration.BeginnZweiteAnkunftsphaseMinuten, PatientenKonfiguration.BeginnDritteAnkunftsphaseMinuten,
                PatientenKonfiguration.ZWISCHENANKUNFT_NAECHSTE_3_STUNDEN_MINUTEN);
            GenerierePhase(ankunftszeiten, rnd, PatientenKonfiguration.BeginnDritteAnkunftsphaseMinuten, SimulationKonfiguration.SIMULATIONSDAUER,
                PatientenKonfiguration.ZWISCHENANKUNFT_LETZTE_3_STUNDEN_MINUTEN);

            int aufnahmekapazitaet = Aufnahmestopp.BerechneArztkapazitaet(SimulationKonfiguration.SIMULATIONSDAUER);
            daten.ErfassePrognoseAufnahmepruefung(env.StartDate, aufnahmestoppMinuten, aufnahmekapazitaet);

            int naechstePatientId = erstePatientId;
            foreach (double ankunftszeit in ankunftszeiten)
            {
                double minutenBisAnkunft = ankunftszeit - AktuelleSimulationszeitMinuten(env);

                if (minutenBisAnkunft > 0)
                    yield return env.Timeout(TimeSpan.FromMinutes(minutenBisAnkunft));

                // Der Generator weist Ankuenfte ab dem Aufnahmestopp bereits vor dem Klinikeintritt ab.
                // Die separate Aufnahmeprognose im Patientenprozess bewertet bereits aktive Patienten.
                if (ankunftszeit >= aufnahmestoppMinuten)
                {
                    double jetztMinuten = AktuelleSimulationszeitMinuten(env);
                    daten.ErfassePrognoseAufnahmeAbgewiesen(env.StartDate, jetztMinuten, naechstePatientId);
                    daten.LogEvent(jetztMinuten, "abgewiesen_vor_klinik_wegen_aufnahmeprognose", naechstePatientId);
                    naechstePatientId++;
                    continue;
                }

                env.Process(erzeugePatientenProzess(naechstePatientId));
                naechstePatientId++;
            }
        }

        private static void GenerierePhase(
            ICollection<double> ankunftszeiten,
            Random rnd,
            double phasenStartMinuten,
            double phasenEndeMinuten,
            double mittlereZwischenankunftMinuten)
        {
            phasenEndeMinuten = Math.Min(phasenEndeMinuten, SimulationKonfiguration.SIMULATIONSDAUER);
            if (phasenEndeMinuten <= phasenStartMinuten)
                return;
            if (mittlereZwischenankunftMinuten <= 0.0)
                throw new InvalidOperationException("Die mittlere Zwischenankunftszeit muss groesser als 0 sein.");

            double ankunftszeitMinuten = phasenStartMinuten;
            double rateProMinute = 1.0 / mittlereZwischenankunftMinuten;
            while (true)
            {
                ankunftszeitMinuten += MathNet.Numerics.Distributions.Exponential.Sample(rnd, rateProMinute);
                if (ankunftszeitMinuten >= phasenEndeMinuten)
                    break;

                ankunftszeiten.Add(ankunftszeitMinuten);
            }
        }
    }
}
