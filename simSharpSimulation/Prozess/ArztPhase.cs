using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    public static class ArztPhase
    {
        private static int GetPriority(PatientenTyp typ)
        {
            return typ switch
            {
                PatientenTyp.Kurz => 1,
                PatientenTyp.Mittel => 2,
                PatientenTyp.Lang => 3,
                _ => 3
            };
        }

        public static IEnumerable<Event> DurchlaufeArzt(
            Simulation env,
            int patientId,
            BeweglicherArztPool aerzte,
            PatientenTyp patientenTyp,
            double ankunftszeit,
            bool hatTermin,
            TimeSpan interneBewegungsdauer,
            double behandlungsdauer,
            SimulationsDaten daten,
            BehandlungsPhaseErgebnis ergebnis)
        {
            double schichtEndeMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            if (nowMinutes >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichArztWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis))
                    yield return ev;
                yield break;
            }

            while (!aerzte.IstFrei)
            {
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double restMinuten = schichtEndeMinuten - nowMinutes;
                if (restMinuten <= 0)
                {
                    foreach (Event ev in BrichArztWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }

                Event arztFrei = aerzte.WennMitarbeiterFreiWird();
                Event schichtEnde = env.Timeout(TimeSpan.FromMinutes(restMinuten));
                yield return arztFrei | schichtEnde;

                if (!arztFrei.IsProcessed)
                {
                    foreach (Event ev in BrichArztWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }
            }

            using (Request req = aerzte.FordereMitarbeiterAn(GetPriority(patientenTyp)))
            {
                yield return req;

                int arztId = aerzte.UebernehmeFreienMitarbeiter();

                daten.ErfasseArztBehandlungBegonnen(env.StartDate);

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "verlaesst_wartezimmer_fuer_arzt", patientId);
                daten.LogEvent(nowMinutes, "geht_zum_arzt", patientId);
                yield return env.Timeout(interneBewegungsdauer);

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "betritt_arztzimmer", patientId);
                daten.LogEvent(nowMinutes, "startet_arzt_behandlung", patientId, arztId: arztId);

                double wartezeitArzt = nowMinutes - ankunftszeit;
                daten.ErfasseArztWartezeit(wartezeitArzt, hatTermin, patientenTyp);
                daten.ErfasseArztBehandlungszeit(behandlungsdauer, hatTermin, patientenTyp);

                yield return env.Timeout(TimeSpan.FromMinutes(behandlungsdauer));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_arzt_behandlung", patientId, arztId: arztId);
                aerzte.GibMitarbeiterZurueck(arztId);
            }
        }

        private static IEnumerable<Event> BrichArztWartenAb(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            int? arztId,
            TimeSpan interneBewegungsdauer,
            BehandlungsPhaseErgebnis ergebnis)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.ErfasseArztAbbruchFeierabend(env.StartDate);
            daten.LogEvent(nowMinutes, "bricht_ab_wegen_feierabend_arzt", patientId, arztId: arztId);

            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(interneBewegungsdauer);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            daten.SchliessePrognosen(patientId, nowMinutes);
            ergebnis.MarkiereKlinikVerlassen();
        }
    }
}
