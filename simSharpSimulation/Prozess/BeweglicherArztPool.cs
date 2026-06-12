using SimSharp;
using System.Collections.Generic;
using System.Reflection;

namespace simSharpSimulation
{
    public sealed class BeweglicherArztPool
    {
        private static readonly FieldInfo? RequestQueueField =
            typeof(PriorityResource).GetField("<RequestQueue>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

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

        public int Frei => ressource.Remaining;

        public int InBenutzung => ressource.InUse;

        public int WarteschlangeLaenge => ErmittleWarteschlangeLaenge();

        public bool IstFrei => ressource.Remaining > 0;

        public Event WennMitarbeiterVerfuegbar()
        {
            return ressource.WhenAny();
        }

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

        private int ErmittleWarteschlangeLaenge()
        {
            object? queue = RequestQueueField?.GetValue(ressource);
            object? count = queue?.GetType().GetProperty("Count")?.GetValue(queue);
            return count is int value ? value : 0;
        }
    }
}
