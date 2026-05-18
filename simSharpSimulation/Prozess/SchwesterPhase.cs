using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik fuer die Schwester-Phase im Simulationsprozess.
     * Die Warteschlange ist analog zur Arzt-Phase aufgebaut:
     * warten auf freie Ressource oder Verschiebung ueber die Schlussplanung.
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

        public static IEnumerable<Event> DurchlaufeSchwester(
            Simulation env,
            int patientId,
            PriorityResource schwester,
            int schwesterId,
            PatientenTyp patientenTyp,
            double ankunftszeit,
            bool hatTermin,
            bool direktZurSchwester,
            bool pruefeVorbereitungNachZimmer,
            TimeSpan interneBewegungsdauer,
            Random rnd,
            SimulationsDaten daten,
            BehandlungsPhaseErgebnis ergebnis)
        {
            double schichtEndeMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            if (!direktZurSchwester)
            {
                daten.LogEvent(nowMinutes, "betritt_schwester_warteschlange", patientId);
            }

            if (nowMinutes >= schichtEndeMinuten)
            {
                foreach (Event ev in VerschiebeWegenSchwesterAufFolgetag(env, daten, patientId, schwesterId, hatTermin, interneBewegungsdauer, ergebnis))
                    yield return ev;
                yield break;
            }

            while (schwester.Remaining <= 0)
            {
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                SchlussplanungsEntscheidung prognose = Schlussplanung.PruefeSchwester(env, schwester, patientenTyp, hatTermin);
                if (prognose.MussVerschobenWerden)
                {
                    foreach (Event ev in VerschiebeWegenSchwesterAufFolgetag(env, daten, patientId, schwesterId, hatTermin, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }

                double restMinuten = schichtEndeMinuten - nowMinutes;
                if (restMinuten <= 0)
                {
                    foreach (Event ev in VerschiebeWegenSchwesterAufFolgetag(env, daten, patientId, schwesterId, hatTermin, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }

                Event schwesterVerfuegbar = schwester.WhenAny();
                Event schichtEnde = env.Timeout(TimeSpan.FromMinutes(restMinuten));
                yield return schwesterVerfuegbar | schichtEnde;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                if (!schwesterVerfuegbar.IsProcessed)
                {
                    foreach (Event ev in VerschiebeWegenSchwesterAufFolgetag(env, daten, patientId, schwesterId, hatTermin, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }
            }

            using (Request req = schwester.Request(priority: GetPriority(patientenTyp)))
            {
                // Request abgeschlossen: Patient hat die Schwester zugewiesen bekommen.
                // Behandlung darf auch nach Schichtende zu Ende gefuehrt werden.
                yield return req;

                if (!direktZurSchwester)
                {
                    daten.LogEvent(nowMinutes, "verlaesst_wartezimmer", patientId);
                }

                daten.LogEvent(nowMinutes, "geht_zur_schwester", patientId);
                yield return env.Timeout(interneBewegungsdauer);

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "betritt_schwesterzimmer", patientId);
                daten.LogEvent(nowMinutes, "startet_schwester_prozess", patientId, schwesterId: schwesterId);

                double wartezeitSchwester = nowMinutes - ankunftszeit;
                daten.ErfasseSchwesterWartezeit(wartezeitSchwester, patientenTyp, hatTermin);

                var typInfo = PatientenKonfiguration.TYPEN_VERTEILUNG.First(t => t.Typ == patientenTyp);
                double mittlereDauer = typInfo.BehandlungszeitSchwester;
                double variationskoeffizient = typInfo.VariationskoeffizientSchwester;

                double varianz = Math.Pow(variationskoeffizient * mittlereDauer, 2);
                double mu = Math.Log(mittlereDauer) - 0.5 * Math.Log(1 + varianz / Math.Pow(mittlereDauer, 2));
                double sigma = Math.Sqrt(Math.Log(1 + varianz / Math.Pow(mittlereDauer, 2)));

                double dauerBehandlung = MathNet.Numerics.Distributions.LogNormal.Sample(rnd, mu, sigma);
                daten.ErfasseSchwesterBehandlungszeit(dauerBehandlung, hatTermin, patientenTyp);
                yield return env.Timeout(TimeSpan.FromMinutes(dauerBehandlung));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_schwester_prozess", patientId, schwesterId: schwesterId);
            }
        }

        private static IEnumerable<Event> VerschiebeWegenSchwesterAufFolgetag(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            int schwesterId,
            bool hatTermin,
            TimeSpan interneBewegungsdauer,
            BehandlungsPhaseErgebnis ergebnis)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.ErfasseSchwesterVerschobenSchlussplanung(env.StartDate);
            string eventTyp = hatTermin
                ? "erhaelt_festen_termin_am_naechsten_vormittag_schwester"
                : "wird_auf_naechsten_tag_verschoben_schwester";
            daten.LogEvent(nowMinutes, eventTyp, patientId, schwesterId: schwesterId);

            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(interneBewegungsdauer);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            ergebnis.MarkiereKlinikVerlassen();
        }
    }
}
