using SimSharp;
using System.Collections.Generic;

namespace simSharpSimulation
{
    public sealed class BeweglicherSchwesterPool
    {
        private readonly Queue<int> freieSchwesterIds;
        private readonly PriorityResource ressource;

        public BeweglicherSchwesterPool(Simulation env, int anzahlSchwestern)
        {
            Anzahl = anzahlSchwestern;
            ressource = new PriorityResource(env, capacity: anzahlSchwestern);
            freieSchwesterIds = new Queue<int>(anzahlSchwestern);

            for (int id = 1; id <= anzahlSchwestern; id++)
            {
                freieSchwesterIds.Enqueue(id);
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
            return freieSchwesterIds.Dequeue();
        }

        public void GibMitarbeiterZurueck(int schwesterId)
        {
            freieSchwesterIds.Enqueue(schwesterId);
        }
    }
}
