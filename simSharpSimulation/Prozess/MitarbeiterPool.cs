using SimSharp;
using System.Collections.Generic;
using System.Reflection;

namespace simSharpSimulation
{
    internal sealed class MitarbeiterPool
    {
        // SimSharp stellt die Laenge der PriorityResource-Warteschlange nicht oeffentlich bereit.
        // Der bestehende Reflection-Zugriff ist hier zentral gekapselt.
        private static readonly FieldInfo? warteschlangenFeld =
            typeof(PriorityResource).GetField("<RequestQueue>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

        // Die PriorityResource verwaltet die Belegung; diese FIFO-Queue ordnet konkrete Mitarbeiter-IDs zu.
        // Eine ID wird erst nach erfolgreicher Ressourcenzuteilung entnommen.
        private readonly Queue<int> freieMitarbeiterIds;
        private readonly PriorityResource ressource;

        internal MitarbeiterPool(Simulation env, int anzahlMitarbeiter)
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
            return freieMitarbeiterIds.Dequeue();
        }

        // Die ID wird vor der anschliessenden Request-Freigabe wieder fuer den naechsten Patienten bereitgestellt.
        public void GibMitarbeiterZurueck(int mitarbeiterId)
        {
            freieMitarbeiterIds.Enqueue(mitarbeiterId);
        }

        private int ErmittleWarteschlangeLaenge()
        {
            object? warteschlange = warteschlangenFeld?.GetValue(ressource);
            object? anzahl = warteschlange?.GetType().GetProperty("Count")?.GetValue(warteschlange);
            return anzahl is int laenge ? laenge : 0;
        }
    }
}
