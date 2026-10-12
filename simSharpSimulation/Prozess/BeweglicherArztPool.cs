using SimSharp;

namespace simSharpSimulation
{
    public sealed class BeweglicherArztPool
    {
        private readonly MitarbeiterPool mitarbeiterPool;

        public BeweglicherArztPool(Simulation env, int anzahlAerzte)
        {
            mitarbeiterPool = new MitarbeiterPool(env, anzahlAerzte);
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

        public void GibMitarbeiterZurueck(int arztId)
        {
            mitarbeiterPool.GibMitarbeiterZurueck(arztId);
        }
    }
}
