using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik für die "Schwester-Phase" im Simulationsprozess.
     * Sie wird als statische Klasse implementiert, da sie keine eigenen Zustandsdaten hält,
     * sondern alle benötigten Informationen als Parameter erhält.
     */
    public static class SchwesterPhase
    {
        private static int GetPriority(PatientenTyp typ)
        {
            return typ switch
            {
                PatientenTyp.Kurz => 3,
                PatientenTyp.Mittel => 2,
                PatientenTyp.Lang => 1,
                _ => 1
            };
        }
        /*
         * Beschreibt den Prozess, den ein Patient bei der Krankenschwester durchläuft.
         *
         * env: Die Simulationsumgebung (Uhr, Ereignis-Scheduler).
         * patientId: Die eindeutige ID des Patienten.
         * schwester: Die Schwester-Ressource, die belegt werden muss.
         * ankunftszeit: Der Zeitpunkt, an dem der Patient die Klinik betreten hat (für Wartezeitberechnung).
         * direktZurSchwester: Gibt an, ob der Patient das Wartezimmer übersprungen hat.
         * pruefeVorbereitungNachZimmer: Steuert, ob eine zufällige Vorbereitung stattfinden soll.
         * rnd: Der Zufallsgenerator für stochastische Dauern.
         * daten: Das Objekt zum Sammeln und Speichern von Simulationsdaten.
         * returns: Eine Sequenz von Simulationsereignissen.
         */
        public static IEnumerable<Event> DurchlaufeSchwester(
            Simulation env,
            int patientId,
            BeweglicherMitarbeiterPool schwestern,
            PatientenTyp patientenTyp,
            double ankunftszeit,
            bool hatTermin,
            bool direktZurSchwester,
            bool pruefeVorbereitungNachZimmer,
            Random rnd,
            SimulationsDaten daten)
        {
            // Aktuelle Simulationszeit für das Logging holen.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // Schritt S1: Wenn der Patient nicht direkt zur Schwester konnte,
            // wird geloggt, dass er die Warteschlange für die Schwester betritt.
            if (!direktZurSchwester)
            {
                daten.LogEvent(nowMinutes, "betritt_schwester_warteschlange", patientId);
            }

            // Schritt S2: Eine Schwester anfordern.
            // 'using' stellt sicher, dass die Ressource am Ende wieder freigegeben wird.
            // Der Prozess pausiert hier (yield return req), bis eine Schwester frei ist.
            using (var req = schwestern.FordereMitarbeiterAn(GetPriority(patientenTyp)))
            {
                yield return req; // Warten, bis die Schwester-Ressource verfügbar ist.

                // Schritt S3: Schwester ist frei, der Prozess wird fortgesetzt.
                int schwesterId = schwestern.UebernehmeFreienMitarbeiter();
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "startet_schwester_prozess", patientId, schwesterId: schwesterId);

                // Die Wartezeit auf die Schwester berechnen und speichern.
                double wartezeitSchwester = nowMinutes - ankunftszeit;
                daten.ErfasseSchwesterWartezeit(wartezeitSchwester, patientenTyp, hatTermin);

                // Schritt S5: Die eigentliche Behandlung/Interaktion mit der Schwester.
                // Dauer der Behandlung basiert auf dem Patienten-Typ.
                var typInfo = PatientenKonfiguration.TYPEN_VERTEILUNG.First(t => t.Typ == patientenTyp);
                double mittlereDauer = typInfo.BehandlungszeitSchwester;
                
                double sigma = 0.35; // Etwas geringere Streuung bei standardisierten Schwester-Aufgaben
                double mu = Math.Log(mittlereDauer) - 0.5 * Math.Pow(sigma, 2);
                double dauerBehandlung = MathNet.Numerics.Distributions.LogNormal.Sample(rnd, mu, sigma);
                daten.ErfasseSchwesterBehandlungszeit(dauerBehandlung, hatTermin, patientenTyp);
                yield return env.Timeout(TimeSpan.FromMinutes(dauerBehandlung)); // Prozess für die Dauer anhalten.

                // Schritt S6: Der gesamte Schwester-Prozess ist beendet.
                // Die Ressource wird durch das 'using'-Statement automatisch freigegeben.
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_schwester_prozess", patientId, schwesterId: schwesterId);
                schwestern.GibMitarbeiterZurueck(schwesterId);
            }
        }
    }
}
