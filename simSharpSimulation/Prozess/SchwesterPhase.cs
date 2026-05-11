using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik fuer die Schwester-Phase im Simulationsprozess.
     * Die Warteschlange ist analog zur Arzt-Phase aufgebaut:
     * warten auf freie Ressource oder Abbruch wegen Wartezeit/Feierabend.
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
            double limitMinuten = 55.0;
            double schichtEndeMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            if (!direktZurSchwester)
            {
                daten.LogEvent(nowMinutes, "betritt_schwester_warteschlange", patientId);
            }

            if (nowMinutes >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, schwesterId, interneBewegungsdauer, wegenFeierabend: true, ergebnis))
                    yield return ev;
                yield break;
            }

            double deadlineMinuten = Math.Min(schichtEndeMinuten, nowMinutes + limitMinuten);

            while (schwester.Remaining <= 0)
            {
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double restMinuten = deadlineMinuten - nowMinutes;

                if (restMinuten <= 0)
                {
                    bool wegenFeierabend = nowMinutes >= schichtEndeMinuten;
                    foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, schwesterId, interneBewegungsdauer, wegenFeierabend, ergebnis))
                        yield return ev;
                    yield break;
                }

                Event schwesterVerfuegbar = schwester.WhenAny();
                Event timeout = env.Timeout(TimeSpan.FromMinutes(restMinuten));
                yield return schwesterVerfuegbar | timeout;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                if (!schwesterVerfuegbar.IsProcessed)
                {
                    bool wegenFeierabend = nowMinutes >= schichtEndeMinuten;
                    foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, schwesterId, interneBewegungsdauer, wegenFeierabend, ergebnis))
                        yield return ev;
                    yield break;
                }
            }

            using (Request req = schwester.Request(priority: GetPriority(patientenTyp)))
            {
                yield return req;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                if (nowMinutes > schichtEndeMinuten)
                {
                    foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, schwesterId, interneBewegungsdauer, wegenFeierabend: true, ergebnis))
                        yield return ev;
                    yield break;
                }

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

                double sigma = 0.35;
                double mu = Math.Log(mittlereDauer) - 0.5 * Math.Pow(sigma, 2);
                double dauerBehandlung = MathNet.Numerics.Distributions.LogNormal.Sample(rnd, mu, sigma);
                daten.ErfasseSchwesterBehandlungszeit(dauerBehandlung, hatTermin, patientenTyp);
                yield return env.Timeout(TimeSpan.FromMinutes(dauerBehandlung));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_schwester_prozess", patientId, schwesterId: schwesterId);
            }
        }

        private static IEnumerable<Event> BrichSchwesterWartenAb(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            int schwesterId,
            TimeSpan interneBewegungsdauer,
            bool wegenFeierabend,
            BehandlungsPhaseErgebnis ergebnis)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            if (wegenFeierabend)
            {
                daten.ErfasseSchwesterAbbruchFeierabend(env.StartDate);
                daten.LogEvent(nowMinutes, "bricht_ab_wegen_feierabend_schwester", patientId, schwesterId: schwesterId);
            }
            else
            {
                daten.ErfasseSchwesterAbbruchWartezeit(env.StartDate);
                daten.LogEvent(nowMinutes, "bricht_ab_und_verlaesst_klinik_wegen_wartezeit_schwester", patientId, schwesterId: schwesterId);
            }

            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(interneBewegungsdauer);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            ergebnis.MarkiereKlinikVerlassen();
        }
    }
}
