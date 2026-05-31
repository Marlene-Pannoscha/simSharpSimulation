namespace simSharpSimulation
{
    internal sealed class RezeptionKonfiguration : PersonenKonfiguration
    {
        public static int ANZAHL_REZEPTIONISTEN { get; internal set; } = 1;
        public static double MITTELREZEPTIONSZEIT { get; internal set; } = 2.0; // durchschnittliche Dauer an der Rezeption in Minuten
        public static double VARIATIONSKOEFFIZIENT_REZEPTION { get; internal set; } = 1.0;

        public override int Anzahl => ANZAHL_REZEPTIONISTEN;
        public override double MittlereServicezeit => MITTELREZEPTIONSZEIT;
        public override string Beschreibung => "Rezeptionisten in der Klinik";
    }
}
