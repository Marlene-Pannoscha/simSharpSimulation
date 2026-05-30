using SimSharp;
using System.Collections.Generic;

namespace simSharpSimulation
{
    public sealed class BeweglicherArztPool
    {
        private readonly Queue<int> freieArztIds;
        private readonly PriorityResource ressource;

        public BeweglicherArztPool(Simulation env, int anzahlAerzte)
        {
            Anzahl = anzahlAerzte;
            ressource = new PriorityResource(env, capacity: anzahlAerzte);
            freieArztIds = new Queue<int>(anzahlAerzte);

            for (int id = 1; id <= anzahlAerzte; id++)
            {
                freieArztIds.Enqueue(id);
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
            return freieArztIds.Dequeue();
        }

        public void GibMitarbeiterZurueck(int arztId)
        {
            freieArztIds.Enqueue(arztId);
        }
    }
}
