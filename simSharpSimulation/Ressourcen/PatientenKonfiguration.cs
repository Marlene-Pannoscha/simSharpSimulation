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
        public static int ANZAHL_PATIENTEN_TAG { get; internal set; } = 80;
        public static double ERWARTUNGSWERT { get; internal set; } = 180.0;
        public static double STANDARDABWEICHUNG { get; internal set; } = 80.0;
        public static double TERMIN_WAHRSCHEINLICHKEIT { get; internal set; } = 0.7;
        public static double TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT { get; internal set; } = 0.4;
        public static double OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT { get; internal set; } = 0.80;
        public static double MITTLERE_WARTEZIMMER_DAUER_SCHWESTER { get; internal set; } = 2.0;
        public static double MITTLERE_WARTEZIMMER_DAUER_ARZT { get; internal set; } = 5.0;
        public static double MIT_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER { get; internal set; } = 0.6;
        public static double OHNE_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER { get; internal set; } = 2.5;
        public static double MIT_TERMIN_WARTEZIMMER_FAKTOR_ARZT { get; internal set; } = 0.40;
        public static double OHNE_TERMIN_WARTEZIMMER_FAKTOR_ARZT { get; internal set; } = 3.0;

        public static (PatientenTyp Typ, double Wahrscheinlichkeit, double BehandlungszeitArzt, double BehandlungszeitSchwester, double Behandlungskosten)[] TYPEN_VERTEILUNG { get; internal set; } = new[]
        {
            (PatientenTyp.Kurz, 0.3, 3.0, 2.0, 18.0),
            (PatientenTyp.Mittel, 0.6, 7.0, 5.0, 35.0),
            (PatientenTyp.Lang, 0.1, 15.0, 10.0, 60.0)
        };

        public override int Anzahl => ANZAHL_PATIENTEN_TAG;
        public override double MittlereServicezeit => ERWARTUNGSWERT;
        public override string Beschreibung => "Patienten in der Klinik";
    }
}
