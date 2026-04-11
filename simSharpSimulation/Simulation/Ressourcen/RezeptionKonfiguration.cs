namespace simSharpSimulation
{
    internal sealed class RezeptionKonfiguration : PersonenKonfiguration
    {
        public const int ANZAHL_REZEPTIONISTEN = 1;
        public const double MITTELREZEPTIONSZEIT = 2.0; // durchschnittliche Dauer an der Rezeption in Minuten

        public override int Anzahl => ANZAHL_REZEPTIONISTEN;
        public override double MittlereServicezeit => MITTELREZEPTIONSZEIT;
        public override string Beschreibung => "Rezeptionisten in der Klinik";
    }
}
