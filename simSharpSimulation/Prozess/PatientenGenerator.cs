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
            Resource rezeption,
            BeweglicherArztPool aerzte,
            BeweglicherSchwesterPool schwestern,
            Random rnd,
            SimulationsDaten daten,
            int erstePatientId,
            Func<Simulation, int, Resource, BeweglicherSchwesterPool, BeweglicherArztPool, IEnumerable<Event>> erzeugePatientenProzess)
        {
            double aufnahmestoppMinuten = BerechneAufnahmestoppZeitpunkt();

            // Exponentielle Zwischenankunftszeiten je Tagesphase ergeben einen
            // inhomogenen Poisson-Prozess mit konstanter Rate pro Phase.
            // Alle Ankuenfte werden vorab gezogen. Diese Reihenfolge der Zufallsziehungen
            // ist fuer reproduzierbare Laeufe mit demselben Seed relevant.
            var ankunftszeiten = new List<double>();
            GenerierePhase(ankunftszeiten, rnd, 0.0, 120.0,
                PatientenKonfiguration.ZWISCHENANKUNFT_ERSTE_2_STUNDEN_MINUTEN);
            GenerierePhase(ankunftszeiten, rnd, 120.0, 300.0,
                PatientenKonfiguration.ZWISCHENANKUNFT_NAECHSTE_3_STUNDEN_MINUTEN);
            GenerierePhase(ankunftszeiten, rnd, 300.0, SimulationKonfiguration.SIMULATIONSDAUER,
                PatientenKonfiguration.ZWISCHENANKUNFT_LETZTE_3_STUNDEN_MINUTEN);

            int aufnahmekapazitaet = BerechneAufnahmestoppKapazitaet();
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

                env.Process(erzeugePatientenProzess(env, naechstePatientId, rezeption, schwestern, aerzte));
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

        private static double BerechneAufnahmestoppZeitpunkt()
        {
            return Math.Max(
                0.0,
                SimulationKonfiguration.SIMULATIONSDAUER -
                SimulationKonfiguration.PROGNOSE_PRUEFUNG_VOR_SCHLIESSUNG_MINUTEN);
        }

        private static int BerechneAufnahmestoppKapazitaet()
        {
            double simulationsdauerMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double mittlereArztBehandlungsdauer = Math.Max(0.1, ArztKonfiguration.MITTLERE_BEHANDLUNGSDAUER);
            int kapazitaet = (int)Math.Floor((simulationsdauerMinuten * ArztKonfiguration.ANZAHL_AERZTE) / mittlereArztBehandlungsdauer);
            return Math.Max(0, kapazitaet);
        }
    }
}
