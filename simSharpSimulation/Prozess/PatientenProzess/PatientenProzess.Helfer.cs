using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimSharp;

namespace simSharpSimulation
{
    // Diese Datei enthält gemeinsam genutzte Hilfsmethoden des Patientenprozesses.
    // Sie ist aktuell wichtig, weil Ressourcenwahl, PatientenTyp-Wahl und Belegungsprüfung
    // aus dem Patientenablauf hierher ausgelagert wurden.
    internal sealed partial class PatientenProzess
    {
        // Hilfsmethode zum Auswählen einer Ressource
        // Strategie:
        // - wenn sofort freie Ressourcen existieren, wähle zufällig unter den freien
        // - sonst wähle zufällig aus allen Ressourcen und warte dort
        // Dadurch vermeiden wir eine starre Bevorzugung immer derselben Ressource.
        private (PriorityResource res, int id) WaehleRessource(List<PriorityResource> ressourcen)
        {
            // Freie Ressourcen werden bevorzugt, sonst wartet der Patient bei einer zufaelligen Ressource.
            var freieRessourcen = ressourcen
                .Select((res, index) => (res, index))
                .Where(eintrag => eintrag.res.Remaining > 0)
                .ToList();

            if (freieRessourcen.Count > 0)
            {
                var eintrag = freieRessourcen[rnd.Next(freieRessourcen.Count)];
                return (eintrag.res, eintrag.index + 1);
            }

            int index = rnd.Next(ressourcen.Count);
            return (ressourcen[index], index + 1);
        }

        // Phase P-C: Delegation an ausgelagerte Phasenklassen.
        // Schritt P8: Interne Hilfsmethode, um Patienten-Typ zu wählen.
        // Es wird kumulativ über die konfigurierten Wahrscheinlichkeiten gelaufen,
        // bis die Zufallszahl in eines der Gewichtsintervalle fällt.
        private static PatientenTyp WaehlePatientenTyp(System.Random rnd)
        {
            double rand = rnd.NextDouble();
            double cumulative = 0.0;
            foreach (var (typ, wahrsch, _, _, _, _, _) in PatientenKonfiguration.TYPEN_VERTEILUNG)
            {
                cumulative += wahrsch;
                if (rand <= cumulative)
                    return typ;
            }
            return PatientenTyp.Mittel; // Fallback
        }

        // Schritt P9: Interne Hilfsmethode, um aktuelle Belegung der Ressource zu prüfen.
        // SimSharp exponiert diese Information nicht direkt in der gewünschten Form,
        // deshalb lesen wir die internen Users per Reflection aus.
        // Der Rückgabewert ist die Zahl gerade belegter Ressourceninstanzen.
        private static int ErmittleAktiveNutzer<T>(List<T> ressourcen)
        {
            return ressourcen.Sum(r => {
                var usersProperty = r?.GetType().GetProperty("Users", BindingFlags.NonPublic | BindingFlags.Instance);
                var usersCollection = usersProperty?.GetValue(r) as IReadOnlyCollection<Request>;
                return usersCollection?.Count ?? 0;
            });
        }
    }
}
