namespace simSharpSimulation
{
    internal sealed class FinanzKonfiguration
    {
        // Diese Werte werden beim Start aus finanz-konfiguration.json geladen.
        public static double ARZT_LOHN_PRO_PATIENT { get; internal set; } = 0.0;
        public static double ARZT_LOHN_PRO_STUNDE { get; internal set; } = 0.0;
        public static double SCHWESTER_LOHN_PRO_STUNDE { get; internal set; } = 0.0;
        public static double REZEPTION_LOHN_PRO_STUNDE { get; internal set; } = 0.0;
        public static double MIETKOSTEN_PRO_TAG { get; internal set; } = 0.0;
        public static double WEITERE_FIXKOSTEN_PRO_TAG { get; internal set; } = 0.0;
        public static int ARBEITSSTUNDEN_PRO_TAG { get; internal set; } = 0;
        public static double ANTEIL_PRIVATVERSICHERT { get; internal set; } = 0.0;
        public static double ANTEIL_GESETZLICH_VERSICHERT => 1.0 - ANTEIL_PRIVATVERSICHERT;
        public static double EINNAHME_PRIVATPATIENT { get; internal set; } = 0.0;
        public static double EINNAHME_GESETZLICH_PATIENT { get; internal set; } = 0.0;
        public static double BEHANDLUNGSKOSTEN_KURZ { get; internal set; } = 0.0;
        public static double BEHANDLUNGSKOSTEN_MITTEL { get; internal set; } = 0.0;
        public static double BEHANDLUNGSKOSTEN_LANG { get; internal set; } = 0.0;
    }
}
