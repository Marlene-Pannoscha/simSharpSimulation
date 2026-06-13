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

    internal sealed class PatientenKonfiguration : PersonenKonfiguration
    {
        public static int ANZAHL_PATIENTEN_TAG { get; internal set; }
        public static double ERWARTUNGSWERT { get; internal set; }
        public static double STANDARDABWEICHUNG { get; internal set; }
        public static double TERMIN_WAHRSCHEINLICHKEIT { get; internal set; }
        public static OhneTerminTagesanteil[] OHNE_TERMIN_TAGESANTEILE { get; internal set; } =
            Array.Empty<OhneTerminTagesanteil>();
        public static double TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT { get; internal set; }
        public static double OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT { get; internal set; }
        public static double MITTLERE_WARTEZIMMER_DAUER_SCHWESTER { get; internal set; }
        public static double MITTLERE_WARTEZIMMER_DAUER_ARZT { get; internal set; }
        public static double MIT_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER { get; internal set; }
        public static double OHNE_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER { get; internal set; }
        public static double MIT_TERMIN_WARTEZIMMER_FAKTOR_ARZT { get; internal set; }
        public static double OHNE_TERMIN_WARTEZIMMER_FAKTOR_ARZT { get; internal set; }
        public static int OHNE_TERMIN_PRIORITAETSZUSCHLAG { get; internal set; }

        public static (PatientenTyp Typ, double Wahrscheinlichkeit, double BehandlungszeitArzt, double VariationskoeffizientArzt, double BehandlungszeitSchwester, double VariationskoeffizientSchwester, double Behandlungskosten)[] TYPEN_VERTEILUNG { get; internal set; } =
            Array.Empty<(PatientenTyp Typ, double Wahrscheinlichkeit, double BehandlungszeitArzt, double VariationskoeffizientArzt, double BehandlungszeitSchwester, double VariationskoeffizientSchwester, double Behandlungskosten)>();

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

        public static int BerechneAnzahlPatientenMitTermin()
        {
            return (int)Math.Round(ANZAHL_PATIENTEN_TAG * TERMIN_WAHRSCHEINLICHKEIT);
        }

        public static int BerechneAnzahlPatientenOhneTermin()
        {
            return Math.Max(0, ANZAHL_PATIENTEN_TAG - BerechneAnzahlPatientenMitTermin());
        }

        public static (PatientenTyp Typ, double Wahrscheinlichkeit, double BehandlungszeitArzt, double VariationskoeffizientArzt, double BehandlungszeitSchwester, double VariationskoeffizientSchwester, double Behandlungskosten) HoleTypInfo(PatientenTyp typ)
        {
            return TYPEN_VERTEILUNG.First(t => t.Typ == typ);
        }

        public override int Anzahl => ANZAHL_PATIENTEN_TAG;
        public override double MittlereServicezeit => ERWARTUNGSWERT;
        public override string Beschreibung => "Patienten in der Klinik";
    }

    internal readonly record struct OhneTerminTagesanteil(
        double VonMinute,
        double BisMinute,
        double Anteil);
}
