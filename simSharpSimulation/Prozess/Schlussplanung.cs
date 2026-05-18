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
     * LOGIK DES PROGNOSEMODELLS FÜR DIE SCHLUSSPLANUNG:
     * 
     * Diese Hilfsklasse bewertet kurz vor Schichtende, ob ein wartender Patient
     * mit hoher Wahrscheinlichkeit noch in die Betriebszeit (Tageskapazität) passt.
     * Falls nicht, wird später in der jeweiligen Phase ein Verschiebe-Event
     * statt eines plötzlichen Feierabend-Abbruchs generiert.
     * 
     * Wie funktioniert das Modell?
     * 1. Zeitfenster: Es wird nur im konfigurierten "Prognosefenster" (z.B. letzte 60 Min) kurz vor Schichtende aktiv.
     * 2. Berechnung: (Anzahl aktiver Patienten + wartender Patienten + 1) * erwartete Dauer / Kapazität.
     * 3. Sicherheit: Ein Sicherheitsfaktor puffert unerwartete Verzögerungen ab (z.B. +10%).
     * 4. Entscheidung: Passt die prognostizierte Zeit nicht mehr in die Restzeit der Schicht, wird der Patient verschoben.
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
            var typInfo = PatientenKonfiguration.HoleTypInfo(patientenTyp);
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
            var typInfo = PatientenKonfiguration.HoleTypInfo(patientenTyp);
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
            // --- Schritt 1: Zeitliche Relevanz prüfen ---
            // Berechne die Restzeit bis zum Ende der Schicht in Minuten.
            // Die Prognose wird nur in einem definierten Fenster vor Schichtende (z.B. letzte Stunde) aktiviert.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            double restMinuten = SimulationKonfiguration.SIMULATIONSDAUER - nowMinutes;
            double prognosefenster = SchlussplanungKonfiguration.PROGNOSEFENSTER_MINUTEN_VOR_SCHLUSS;

            // Wenn die Prognosefunktion deaktiviert ist oder das Zeitfenster noch nicht erreicht wurde,
            // wird die Prüfung sofort abgebrochen und der Patient darf planmäßig warten.
            if (!SchlussplanungKonfiguration.AKTIVIERT || restMinuten > prognosefenster)
            {
                return new SchlussplanungsEntscheidung(false, string.Empty, string.Empty, restMinuten, 0.0);
            }

            // --- Schritt 2: Aktuelle Auslastung der Ressource ermitteln ---
            // Wir müssen wissen, wie lange alle bereits anwesenden Patienten sowie der aktuelle Patient benötigen.
            RessourcenAuslastung auslastung = ErmittleAuslastung(ressource);

            // --- Schritt 3: Prognostizierte Restdauer berechnen ---
            // Logik: 
            // (Aktive Patienten + Wartende + 1 für den neuen Patienten) * Behandlungsdauer je Patient
            // Das Ganze wird durch die Kapazität (Zahl der Behandler) geteilt und mit einem Sicherheitsfaktor multipliziert,
            // um einen Puffer für unerwartete Verzögerungen einzubauen.
            double erwarteteGesamtzeit = BerechnePrognostizierteMinuten(
                auslastung,
                erwarteteBehandlungsdauerMinuten);

            // --- Schritt 4: Entscheidung treffen ---
            // Falls die kalkulierte Gesamtdauer noch in die restliche Arbeitszeit passt, 
            // bleibt der Patient normal in der aktuellen Warteschlange.
            if (erwarteteGesamtzeit <= restMinuten)
            {
                return new SchlussplanungsEntscheidung(false, string.Empty, string.Empty, restMinuten, erwarteteGesamtzeit);
            }

            // Wenn die Zeit nicht reicht: Der Patient wird verschoben.
            // Terminpatienten bekommen bevorzugte Behandlung (z.B. festen Termin direkt am nächsten Vormittag),
            // Patienten ohne Termin werden allgemein auf den nächsten Tag verschoben.
            string eventTyp = hatTermin
                ? $"erhaelt_festen_termin_am_naechsten_vormittag_{bereich}"
                : $"wird_auf_naechsten_tag_verschoben_{bereich}";

            string hinweis = hatTermin
                ? $"naechster Vormittag {SchlussplanungKonfiguration.VORMITTAGS_TERMIN_STUNDE:00}:{SchlussplanungKonfiguration.VORMITTAGS_TERMIN_MINUTE:00}"
                : "naechster Behandlungstag";

            return new SchlussplanungsEntscheidung(true, eventTyp, hinweis, restMinuten, erwarteteGesamtzeit);
        }

        private static RessourcenAuslastung ErmittleAuslastung(object ressource)
        {
            return new RessourcenAuslastung(
                ErmittleKapazitaet(ressource),
                ErmittleAnzahlAusEigenschaft(ressource, "Users"),
                ErmittleAnzahlAusEigenschaft(ressource, "Queue"));
        }

        private static double BerechnePrognostizierteMinuten(
            RessourcenAuslastung auslastung,
            double erwarteteBehandlungsdauerMinuten)
        {
            double sicherheitsfaktor = Math.Max(1.0, SchlussplanungKonfiguration.SICHERHEITSFAKTOR);
            int patientenInPrognose = auslastung.AktiveNutzer + auslastung.WartendePatienten + 1;
            int wirksameKapazitaet = Math.Max(1, auslastung.Kapazitaet);

            return patientenInPrognose
                * erwarteteBehandlungsdauerMinuten
                / wirksameKapazitaet
                * sicherheitsfaktor;
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

        private readonly record struct RessourcenAuslastung(
            int Kapazitaet,
            int AktiveNutzer,
            int WartendePatienten);
    }
}
