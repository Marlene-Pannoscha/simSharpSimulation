namespace simSharpSimulation
{
    public static class SchwesterKonfiguration
    {
        // Werte werden von KonfigurationJsonExport.LadeAlle() gesetzt.
        public static int ANZAHL_SCHWESTERN { get; set; }
        public static RollenwechselOptionen Rollenwechsel { get; set; } = new();
        public static double MITTLERE_BEHANDLUNGSDAUER { get; set; }
    }
}
