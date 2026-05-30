using SimSharp;
using System.Collections.Generic;

namespace simSharpSimulation
{
    public sealed class BeweglicherMitarbeiterPool
    {
        private readonly Queue<int> freieMitarbeiterIds;
        private readonly PriorityResource ressource;

        public BeweglicherMitarbeiterPool(Simulation env, int anzahlMitarbeiter)
        {
            Anzahl = anzahlMitarbeiter;
            ressource = new PriorityResource(env, capacity: anzahlMitarbeiter);
            freieMitarbeiterIds = new Queue<int>(anzahlMitarbeiter);

            for (int id = 1; id <= anzahlMitarbeiter; id++)
            {
                freieMitarbeiterIds.Enqueue(id);
            }
        }

        public int Anzahl { get; }

        public bool IstFrei => ressource.Remaining > 0;

        public Request FordereMitarbeiterAn(int prioritaet)
        {
            return ressource.Request(priority: prioritaet);
        }

        public int UebernehmeFreienMitarbeiter()
        {
            return freieMitarbeiterIds.Dequeue();
        }

        public void GibMitarbeiterZurueck(int mitarbeiterId)
        {
            freieMitarbeiterIds.Enqueue(mitarbeiterId);
        }
    }
}
