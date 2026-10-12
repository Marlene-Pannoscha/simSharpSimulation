using SimSharp;

namespace simSharpSimulation
{
    public sealed class BeweglicherSchwesterPool
    {
        private readonly MitarbeiterPool mitarbeiterPool;

        public BeweglicherSchwesterPool(Simulation env, int anzahlSchwestern)
        {
            mitarbeiterPool = new MitarbeiterPool(env, anzahlSchwestern);
        }

        public int Anzahl => mitarbeiterPool.Anzahl;
        public int Frei => mitarbeiterPool.Frei;
        public int InBenutzung => mitarbeiterPool.InBenutzung;
        public int WarteschlangeLaenge => mitarbeiterPool.WarteschlangeLaenge;
        public bool IstFrei => mitarbeiterPool.IstFrei;

        public Event WennMitarbeiterVerfuegbar()
        {
            return mitarbeiterPool.WennMitarbeiterVerfuegbar();
        }

        public Request FordereMitarbeiterAn(int prioritaet)
        {
            return mitarbeiterPool.FordereMitarbeiterAn(prioritaet);
        }

        public int UebernehmeFreienMitarbeiter()
        {
            return mitarbeiterPool.UebernehmeFreienMitarbeiter();
        }

        public void GibMitarbeiterZurueck(int schwesterId)
        {
            mitarbeiterPool.GibMitarbeiterZurueck(schwesterId);
        }
    }
}
