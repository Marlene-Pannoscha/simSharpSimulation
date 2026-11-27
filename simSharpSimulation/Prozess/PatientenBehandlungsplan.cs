using System;

namespace simSharpSimulation
{
    internal readonly record struct PatientenBehandlungsplan(
        bool HatTermin,
        double ErsteRezeptionsdauer,
        double ZweiteRezeptionsdauer,
        double SchwesterBehandlungsdauer,
        double ArztBehandlungsdauer,
        double SchwesterWartezimmerdauer,
        double ArztWartezimmerdauer,
        bool BrauchtVorbereitung,
        bool GehtNachArztZurRezeption)
    {
        internal static PatientenBehandlungsplan Ziehen(PatientenTyp patientenTyp, Random rnd)
        {
            bool hatTermin = rnd.NextDouble() < PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT;
            // Dauern und Pfadentscheidungen werden einmal vorab gezogen, auch fuer spaeter nicht genutzte Stationen.
            // Prognose und Ablauf verwenden dieselben Werte; die Ziehungsreihenfolge muss erhalten bleiben.
            double ersteRezeptionsdauer = ZieheRezeptionsdauer(rnd);
            double zweiteRezeptionsdauer = ZieheRezeptionsdauer(rnd);
            double schwesterBehandlungsdauer = ZieheSchwesterBehandlungsdauer(patientenTyp, rnd);
            double arztBehandlungsdauer = ZieheArztBehandlungsdauer(patientenTyp, rnd);
            double schwesterWartezimmerdauer = ZieheSchwesterWartezimmerdauer(hatTermin, rnd);
            double arztWartezimmerdauer = ZieheArztWartezimmerdauer(hatTermin, rnd);
            double vorbereitungsWahrscheinlichkeit = hatTermin
                ? PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT
                : PatientenKonfiguration.OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT;
            bool brauchtVorbereitung = rnd.NextDouble() < vorbereitungsWahrscheinlichkeit;
            bool gehtNachArztZurRezeption = rnd.NextDouble() < SimulationKonfiguration.WAHRSCHEINLICHKEIT_NACH_ARZT_ZUR_REZEPTION;
            return new PatientenBehandlungsplan(hatTermin, ersteRezeptionsdauer, zweiteRezeptionsdauer,
                schwesterBehandlungsdauer, arztBehandlungsdauer, schwesterWartezimmerdauer,
                arztWartezimmerdauer, brauchtVorbereitung, gehtNachArztZurRezeption);
        }

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
            var typInfo = PatientenKonfiguration.HoleTypInfo(patientenTyp);
            return ZieheLogNormalDauer(
                typInfo.BehandlungszeitSchwester,
                typInfo.VariationskoeffizientSchwester,
                rnd);
        }

        private static double ZieheArztBehandlungsdauer(PatientenTyp patientenTyp, Random rnd)
        {
            var typInfo = PatientenKonfiguration.HoleTypInfo(patientenTyp);
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
