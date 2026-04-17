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
        public const int ANZAHL_PATIENTEN_TAG = 100; // erwartete Patienten pro Tag
        public const double ERWARTUNGSWERT = 180.0; // Zeitpunkt des Patientengipfels
        public const double STANDARDABWEICHUNG = 80.0; // Streuung der Ankunftszeiten
        public const double TERMIN_WAHRSCHEINLICHKEIT = 0.5; // Wahrscheinlichkeit, dass ein Patient einen Termin hat
        public const double TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT = 0.4; // Wahrscheinlichkeit, dass Terminpatienten initial eine Schwester-Vorbereitung benötigen
        public const double SCHWESTERZIMMER_VORBEREITUNG_WAHRSCHEINLICHKEIT = 0.5; // Wahrscheinlichkeit, dass Patienten nach dem Schwesternzimmer Vorbereitung brauchen
        public const double MITTLERE_WARTEZIMMER_DAUER_SCHWESTER = 2.0; // durchschnittliche Dauer im Wartezimmer für die Schwester in Minuten
        public const double MITTLERE_WARTEZIMMER_DAUER_ARZT = 5.0; // durchschnittliche Dauer im Wartezimmer für den Arzt in Minuten

        // Patiententypen Verteilung
        public static readonly (PatientenTyp Typ, double Wahrscheinlichkeit, double BehandlungszeitArzt, double BehandlungszeitSchwester)[] TYPEN_VERTEILUNG = new[]
        {
            (PatientenTyp.Kurz, 0.3, 3.0, 2.0),   // 30% kurz: Arzt 3 min, Schwester 2 min
            (PatientenTyp.Mittel, 0.6, 7.0, 5.0), // 60% mittel: Arzt 7 min, Schwester 5 min
            (PatientenTyp.Lang, 0.1, 15.0, 10.0)  // 10% lang: Arzt 15 min, Schwester 10 min
        };

        public override int Anzahl => ANZAHL_PATIENTEN_TAG;
        public override double MittlereServicezeit => ERWARTUNGSWERT;
        public override string Beschreibung => "Patienten in der Klinik";
    }
}
