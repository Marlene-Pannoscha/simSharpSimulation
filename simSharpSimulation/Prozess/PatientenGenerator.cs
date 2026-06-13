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
            int terminPatienten = PatientenKonfiguration.BerechneAnzahlPatientenMitTermin();
            int ohneTerminPatienten = PatientenKonfiguration.BerechneAnzahlPatientenOhneTermin();

            for (int i = 0; i < terminPatienten; i++)
            {
                double zeit = ZieheTerminAnkunftszeit(rnd);
                ankunftszeiten.Add(new Ankunft(zeit, true, drawIndex));
                drawIndex++;
            }

            foreach (double zeit in ErzeugeOhneTerminAnkunftszeiten(rnd, ohneTerminPatienten))
            {
                ankunftszeiten.Add(new Ankunft(zeit, false, drawIndex));
                drawIndex++;
            }

            return ankunftszeiten;
        }

        private static double ZieheTerminAnkunftszeit(Random rnd)
        {
            for (int versuch = 0; versuch < 1000; versuch++)
            {
                double zeit = MathNet.Numerics.Distributions.Normal.Sample(
                    rnd,
                    PatientenKonfiguration.ERWARTUNGSWERT,
                    PatientenKonfiguration.STANDARDABWEICHUNG);

                if (zeit <= SimulationKonfiguration.SIMULATIONSDAUER)
                {
                    return zeit;
                }
            }

            return SimulationKonfiguration.SIMULATIONSDAUER;
        }

        private static IEnumerable<double> ErzeugeOhneTerminAnkunftszeiten(Random rnd, int anzahlOhneTermin)
        {
            if (anzahlOhneTermin <= 0)
            {
                yield break;
            }

            foreach (var fenster in VerteileOhneTerminPatientenAufTagesanteile(anzahlOhneTermin))
            {
                if (fenster.Anzahl <= 0)
                {
                    continue;
                }

                foreach (double zeit in ErzeugeExponentialVerteilteZeitenImFenster(
                    rnd,
                    fenster.VonMinute,
                    fenster.BisMinute,
                    fenster.Anzahl))
                {
                    yield return zeit;
                }
            }
        }

        private static List<OhneTerminFenster> VerteileOhneTerminPatientenAufTagesanteile(int anzahlOhneTermin)
        {
            var anteile = PatientenKonfiguration.OHNE_TERMIN_TAGESANTEILE
                .Where(a => a.BisMinute > a.VonMinute && a.Anteil > 0.0)
                .ToArray();

            if (anteile.Length == 0)
            {
                anteile = new[]
                {
                    new OhneTerminTagesanteil(0.0, SimulationKonfiguration.SIMULATIONSDAUER, 1.0)
                };
            }

            double anteilSumme = anteile.Sum(a => a.Anteil);
            var fenster = anteile
                .Select(a =>
                {
                    double exakt = anzahlOhneTermin * (a.Anteil / anteilSumme);
                    int basis = (int)Math.Floor(exakt);
                    return new OhneTerminFenster(
                        a.VonMinute,
                        Math.Min(a.BisMinute, SimulationKonfiguration.SIMULATIONSDAUER),
                        basis,
                        exakt - basis);
                })
                .ToList();

            int rest = anzahlOhneTermin - fenster.Sum(f => f.Anzahl);
            foreach (var index in fenster
                .Select((f, i) => (Fenster: f, Index: i))
                .OrderByDescending(e => e.Fenster.Restanteil)
                .Take(rest)
                .Select(e => e.Index))
            {
                OhneTerminFenster f = fenster[index];
                fenster[index] = f with { Anzahl = f.Anzahl + 1 };
            }

            return fenster;
        }

        private static IEnumerable<double> ErzeugeExponentialVerteilteZeitenImFenster(
            Random rnd,
            double vonMinute,
            double bisMinute,
            int anzahl)
        {
            double dauer = Math.Max(0.0, bisMinute - vonMinute);
            if (dauer <= 0.0 || anzahl <= 0)
            {
                yield break;
            }

            double rate = Math.Max(0.0001, anzahl / dauer);
            double[] abstaende = Enumerable
                .Range(0, anzahl + 1)
                .Select(_ => MathNet.Numerics.Distributions.Exponential.Sample(rnd, rate))
                .ToArray();
            double summe = abstaende.Sum();
            double kumuliert = 0.0;

            for (int i = 0; i < anzahl; i++)
            {
                kumuliert += abstaende[i];
                yield return vonMinute + (kumuliert / summe) * dauer;
            }
        }

        private readonly record struct Ankunft(double Zeit, bool HatTermin, int DrawIndex);
        private readonly record struct OhneTerminFenster(
            double VonMinute,
            double BisMinute,
            int Anzahl,
            double Restanteil);
    }
}
