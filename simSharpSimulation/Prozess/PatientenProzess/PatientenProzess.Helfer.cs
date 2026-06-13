using System;
using System.Linq;

namespace simSharpSimulation
{
    // Diese Datei enthaelt gemeinsam genutzte Zufalls- und Dauer-Hilfsmethoden
    // des Patientenprozesses.
    internal sealed partial class PatientenProzess
    {
        // Zieht konkrete Behandlungsdauern vorab, damit Prognose und Simulation dieselben
        // Lognormal-Ausreisser verwenden.
        private static double ZieheLogNormalDauer(double mittelwert, double variationskoeffizient, Random rnd)
        {
            double varianz = Math.Pow(variationskoeffizient * mittelwert, 2);
            double mu = Math.Log(mittelwert) - 0.5 * Math.Log(1 + varianz / Math.Pow(mittelwert, 2));
            double sigma = Math.Sqrt(Math.Log(1 + varianz / Math.Pow(mittelwert, 2)));

            return MathNet.Numerics.Distributions.LogNormal.Sample(rnd, mu, sigma);
        }

        private static double ZieheRezeptionsdauer(Random rnd)
        {
            return ZieheLogNormalDauer(
                RezeptionKonfiguration.MITTELREZEPTIONSZEIT,
                RezeptionKonfiguration.VARIATIONSKOEFFIZIENT_REZEPTION,
                rnd);
        }

        private static double ZieheSchwesterBehandlungsdauer(PatientenTyp patientenTyp, Random rnd)
        {
            var typInfo = PatientenKonfiguration.TYPEN_VERTEILUNG.First(t => t.Typ == patientenTyp);
            return ZieheLogNormalDauer(
                typInfo.BehandlungszeitSchwester,
                typInfo.VariationskoeffizientSchwester,
                rnd);
        }

        private static double ZieheArztBehandlungsdauer(PatientenTyp patientenTyp, Random rnd)
        {
            var typInfo = PatientenKonfiguration.TYPEN_VERTEILUNG.First(t => t.Typ == patientenTyp);
            return ZieheLogNormalDauer(
                typInfo.BehandlungszeitArzt,
                typInfo.VariationskoeffizientArzt,
                rnd);
        }

        private static double ZieheSchwesterWartezimmerdauer(bool hatTermin, Random rnd)
        {
            double faktor = hatTermin
                ? PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER
                : PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER;

            return MathNet.Numerics.Distributions.Exponential.Sample(
                rnd,
                1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_SCHWESTER * faktor));
        }

        private static double ZieheArztWartezimmerdauer(bool hatTermin, Random rnd)
        {
            double faktor = hatTermin
                ? PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_ARZT
                : PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_ARZT;

            return MathNet.Numerics.Distributions.Exponential.Sample(
                rnd,
                1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_ARZT * faktor));
        }

    }
}
