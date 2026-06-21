using System;
using System.Linq;

namespace simSharpSimulation
{
    public enum PatientenTyp
    {
        Kurz,
        Mittel,
        Lang
    }

    internal static class PatientenKonfiguration
    {
        public static double ZWISCHENANKUNFT_ERSTE_2_STUNDEN_MINUTEN { get; internal set; }
        public static double ZWISCHENANKUNFT_NAECHSTE_3_STUNDEN_MINUTEN { get; internal set; }
        public static double ZWISCHENANKUNFT_LETZTE_3_STUNDEN_MINUTEN { get; internal set; }
        public static double TERMIN_WAHRSCHEINLICHKEIT { get; internal set; } = 0.7;
        public static double TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT { get; internal set; } = 0.4;
        public static double OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT { get; internal set; } = 0.80;
        public static double MITTLERE_WARTEZIMMER_DAUER_SCHWESTER { get; internal set; } = 2.0;
        public static double MITTLERE_WARTEZIMMER_DAUER_ARZT { get; internal set; } = 5.0;
        public static double MIT_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER { get; internal set; } = 0.6;
        public static double OHNE_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER { get; internal set; } = 2.5;
        public static double MIT_TERMIN_WARTEZIMMER_FAKTOR_ARZT { get; internal set; } = 0.40;
        public static double OHNE_TERMIN_WARTEZIMMER_FAKTOR_ARZT { get; internal set; } = 3.0;

        public static (PatientenTyp Typ, double Wahrscheinlichkeit, double BehandlungszeitArzt, double VariationskoeffizientArzt, double BehandlungszeitSchwester, double VariationskoeffizientSchwester, double Behandlungskosten)[] TYPEN_VERTEILUNG { get; internal set; } = new[]
        {
            (PatientenTyp.Kurz, 0.3, 3.0, 0.5, 2.0, 0.4, 18.0),
            (PatientenTyp.Mittel, 0.6, 7.0, 0.4, 5.0, 0.3, 35.0),
            (PatientenTyp.Lang, 0.1, 15.0, 0.3, 10.0, 0.2, 60.0)
        };

        public static PatientenTyp WaehlePatientenTyp(Random rnd)
        {
            double rand = rnd.NextDouble();
            double kumulierteWahrscheinlichkeit = 0.0;

            foreach (var typInfo in TYPEN_VERTEILUNG)
            {
                kumulierteWahrscheinlichkeit += typInfo.Wahrscheinlichkeit;
                if (rand <= kumulierteWahrscheinlichkeit)
                    return typInfo.Typ;
            }

            return PatientenTyp.Mittel;
        }

        public static (PatientenTyp Typ, double Wahrscheinlichkeit, double BehandlungszeitArzt, double VariationskoeffizientArzt, double BehandlungszeitSchwester, double VariationskoeffizientSchwester, double Behandlungskosten) HoleTypInfo(PatientenTyp typ)
        {
            return TYPEN_VERTEILUNG.First(t => t.Typ == typ);
        }

        public static double BerechneErwarteteAnkuenfte(double simulationsdauerMinuten)
        {
            double dauer = Math.Max(0.0, simulationsdauerMinuten);
            double erstePhase = Math.Min(dauer, 120.0) / ZWISCHENANKUNFT_ERSTE_2_STUNDEN_MINUTEN;
            double zweitePhase = Math.Min(Math.Max(dauer - 120.0, 0.0), 180.0)
                / ZWISCHENANKUNFT_NAECHSTE_3_STUNDEN_MINUTEN;
            double drittePhase = Math.Max(dauer - 300.0, 0.0)
                / ZWISCHENANKUNFT_LETZTE_3_STUNDEN_MINUTEN;
            return erstePhase + zweitePhase + drittePhase;
        }
    }
}
