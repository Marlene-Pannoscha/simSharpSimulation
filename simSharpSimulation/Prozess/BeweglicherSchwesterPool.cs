using SimSharp;
using System.Collections.Generic;
using System.Reflection;

namespace simSharpSimulation
{
    public sealed class BeweglicherSchwesterPool
    {
        private static readonly FieldInfo? RequestQueueField =
            typeof(PriorityResource).GetField("<RequestQueue>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

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
            return freieSchwesterIds.Dequeue();
        }

        public void GibMitarbeiterZurueck(int schwesterId)
        {
            freieSchwesterIds.Enqueue(schwesterId);
        }

        private int ErmittleWarteschlangeLaenge()
        {
            object? queue = RequestQueueField?.GetValue(ressource);
            object? count = queue?.GetType().GetProperty("Count")?.GetValue(queue);
            return count is int value ? value : 0;
        }
    }
}
