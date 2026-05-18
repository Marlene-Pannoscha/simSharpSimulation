using SimSharp;
using System.Reflection;

namespace simSharpSimulation
{
    // Ergebnis einer Kapazitaetspruefung kurz vor Schichtende.
    internal readonly record struct SchlussplanungsEntscheidung(
        bool MussVerschobenWerden,
        string EventTyp,
        string HinweisText,
        double RestMinuten,
        double PrognostizierteMinuten);

    /*
     * Diese Hilfsklasse bewertet kurz vor Schichtende, ob ein wartender Patient
     * mit hoher Wahrscheinlichkeit noch in die aktuelle Tageskapazitaet passt.
     * Falls nicht, wird spaeter in der jeweiligen Phase ein Verschiebe-Event
     * statt eines harten Feierabend-Abbruchs ausgeloggt.
     */
    internal static class Schlussplanung
    {
        public static SchlussplanungsEntscheidung PruefeRezeption(
            Simulation env,
            Resource rezeption,
            bool hatTermin)
        {
            return PruefeKapazitaet(
                env,
                rezeption,
                RezeptionKonfiguration.MITTELREZEPTIONSZEIT,
                hatTermin,
                "rezeption");
        }

        public static SchlussplanungsEntscheidung PruefeSchwester(
            Simulation env,
            PriorityResource schwester,
            PatientenTyp patientenTyp,
            bool hatTermin)
        {
            var typInfo = PatientenKonfiguration.TYPEN_VERTEILUNG.First(t => t.Typ == patientenTyp);
            return PruefeKapazitaet(
                env,
                schwester,
                typInfo.BehandlungszeitSchwester,
                hatTermin,
                "schwester");
        }

        public static SchlussplanungsEntscheidung PruefeArzt(
            Simulation env,
            PriorityResource arzt,
            PatientenTyp patientenTyp,
            bool hatTermin)
        {
            var typInfo = PatientenKonfiguration.TYPEN_VERTEILUNG.First(t => t.Typ == patientenTyp);
            return PruefeKapazitaet(
                env,
                arzt,
                typInfo.BehandlungszeitArzt,
                hatTermin,
                "arzt");
        }

        private static SchlussplanungsEntscheidung PruefeKapazitaet(
            Simulation env,
            object ressource,
            double erwarteteBehandlungsdauerMinuten,
            bool hatTermin,
            string bereich)
        {
            // Restzeit bis Tagesende bestimmen. Die Prognose wird nur in einem
            // konfigurierbaren Fenster vor Schichtende aktiviert.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            double restMinuten = SimulationKonfiguration.SIMULATIONSDAUER - nowMinutes;
            double prognosefenster = SchlussplanungKonfiguration.PROGNOSEFENSTER_MINUTEN_VOR_SCHLUSS;

            if (!SchlussplanungKonfiguration.AKTIVIERT || restMinuten > prognosefenster)
            {
                return new SchlussplanungsEntscheidung(false, string.Empty, string.Empty, restMinuten, 0.0);
            }

            // Belegte Ressourcen + Warteschlange + aktueller Patient ergeben die
            // erwartete Zeit bis zum fruehesten moeglichen Abschluss.
            int kapazitaet = ErmittleKapazitaet(ressource);
            int aktiveNutzer = ErmittleAnzahlAusEigenschaft(ressource, "Users");
            int wartendePatienten = ErmittleAnzahlAusEigenschaft(ressource, "Queue");

            double sicherheitsfaktor = Math.Max(1.0, SchlussplanungKonfiguration.SICHERHEITSFAKTOR);
            double erwarteteGesamtzeit = ((aktiveNutzer + wartendePatienten + 1) * erwarteteBehandlungsdauerMinuten / Math.Max(1, kapazitaet))
                * sicherheitsfaktor;

            // Falls die prognostizierte Zeit noch in die Restzeit passt,
            // bleibt der Patient normal in der aktuellen Queue.
            if (erwarteteGesamtzeit <= restMinuten)
            {
                return new SchlussplanungsEntscheidung(false, string.Empty, string.Empty, restMinuten, erwarteteGesamtzeit);
            }

            // Terminpatienten bekommen einen festen Vormittagstermin,
            // Patienten ohne Termin werden auf den naechsten Tag verschoben.
            string eventTyp = hatTermin
                ? $"erhaelt_festen_termin_am_naechsten_vormittag_{bereich}"
                : $"wird_auf_naechsten_tag_verschoben_{bereich}";

            string hinweis = hatTermin
                ? $"naechster Vormittag {SchlussplanungKonfiguration.VORMITTAGS_TERMIN_STUNDE:00}:{SchlussplanungKonfiguration.VORMITTAGS_TERMIN_MINUTE:00}"
                : "naechster Behandlungstag";

            return new SchlussplanungsEntscheidung(true, eventTyp, hinweis, restMinuten, erwarteteGesamtzeit);
        }

        private static int ErmittleKapazitaet(object ressorce)
        {
            PropertyInfo? capacityProperty = ressorce.GetType().GetProperty("Capacity");
            object? value = capacityProperty?.GetValue(ressorce);
            return value is int kapazitaet ? kapazitaet : 1;
        }

        private static int ErmittleAnzahlAusEigenschaft(object ressorce, string propertyName)
        {
            PropertyInfo? property = ressorce.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            object? collection = property?.GetValue(ressorce);
            return collection switch
            {
                System.Collections.ICollection col => col.Count,
                IReadOnlyCollection<Request> requests => requests.Count,
                _ => 0
            };
        }
    }
}
