namespace simSharpSimulation
{
    internal sealed class PatientenKonfiguration : PersonenKonfiguration
    {
        public const int ANZAHL_PATIENTEN_TAG = 100; // erwartete Patienten pro Tag
        public const double ERWARTUNGSWERT = 180.0; // Zeitpunkt des Patientengipfels
        public const double STANDARDABWEICHUNG = 80.0; // Streuung der Ankunftszeiten
        public const double TERMIN_WAHRSCHEINLICHKEIT = 0.5; // Wahrscheinlichkeit, dass ein Patient einen Termin hat
        public const double TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT = 0.4; // Wahrscheinlichkeit, dass Terminpatienten initial eine Schwester-Vorbereitung benötigen
        public const double SCHWESTERZIMMER_VORBEREITUNG_WAHRSCHEINLICHKEIT = 0.5; // Wahrscheinlichkeit, dass Patienten nach dem Schwesternzimmer Vorbereitung brauchen
        public const double MITTLERE_WARTEZIMMER_DAUER = 15.0; // durchschnittliche Dauer im Wartezimmer in Minuten

        public override int Anzahl => ANZAHL_PATIENTEN_TAG;
        public override double MittlereServicezeit => ERWARTUNGSWERT;
        public override string Beschreibung => "Patienten in der Klinik";
    }
}
