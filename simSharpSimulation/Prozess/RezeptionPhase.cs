using SimSharp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik fuer die Rezeptions-Phase.
     * Die Warteschlange ist analog zu Arzt und Schwester begrenzt:
     * warten auf freie Ressource oder Verschiebung ueber die Schlussplanung.
     */
    public static class RezeptionPhase
    {
        public static IEnumerable<Event> DurchlaufeRezeption(
            Simulation env,
            int patientId,
            Resource rezeption,
            double ankunftszeit,
            bool hatTermin,
            bool behandlungBereitsFertig,
            TimeSpan interneBewegungsdauer,
            Random rnd,
            SimulationsDaten daten,
            BehandlungsPhaseErgebnis ergebnis)
        {
            double schichtEndeMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            daten.LogEvent(nowMinutes, "betritt_rezeption_warteschlange", patientId);

            bool rezeptionWarFrei = IstRezeptionFrei(rezeption);
            daten.LogEvent(nowMinutes, rezeptionWarFrei ? "rezeption_frei" : "rezeption_nicht_frei", patientId);
            if (!rezeptionWarFrei)
            {
                daten.LogEvent(nowMinutes, "wartet_in_rezeption_warteschlange", patientId);
            }

            if (nowMinutes >= schichtEndeMinuten)
            {
                foreach (Event ev in VerschiebeWegenRezeptionAufFolgetag(env, daten, patientId, hatTermin, interneBewegungsdauer, ergebnis))
                    yield return ev;
                yield break;
            }

            SchlussplanungsEntscheidung prognose = Schlussplanung.PruefeRezeption(env, rezeption, hatTermin);
            if (prognose.MussVerschobenWerden)
            {
                foreach (Event ev in VerschiebeWegenRezeptionAufFolgetag(env, daten, patientId, hatTermin, interneBewegungsdauer, ergebnis))
                    yield return ev;
                yield break;
            }

            using (Request req = rezeption.Request())
            {
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double restMinuten = schichtEndeMinuten - nowMinutes;

                if (restMinuten <= 0)
                {
                    foreach (Event ev in VerschiebeWegenRezeptionAufFolgetag(env, daten, patientId, hatTermin, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }

                Event schichtEnde = env.Timeout(TimeSpan.FromMinutes(restMinuten));
                yield return req | schichtEnde;

                if (!req.IsProcessed)
                {
                    foreach (Event ev in VerschiebeWegenRezeptionAufFolgetag(env, daten, patientId, hatTermin, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                if (!rezeptionWarFrei)
                {
                    daten.LogEvent(nowMinutes, "rezeption_frei", patientId);
                }

                daten.LogEvent(nowMinutes, "betritt_rezeption", patientId);
                daten.LogEvent(nowMinutes, behandlungBereitsFertig ? "behandlung_bereits_fertig" : "behandlung_nicht_fertig", patientId);
                daten.LogEvent(nowMinutes, "startet_rezeption", patientId);

                double wartezeitRezeption = nowMinutes - ankunftszeit;
                daten.ErfasseRezeptionWartezeit(wartezeitRezeption, hatTermin);

                double mittlereDauer = RezeptionKonfiguration.MITTELREZEPTIONSZEIT;
                double variationskoeffizient = RezeptionKonfiguration.VARIATIONSKOEFFIZIENT_REZEPTION;

                double varianz = Math.Pow(variationskoeffizient * mittlereDauer, 2);
                double mu = Math.Log(mittlereDauer) - 0.5 * Math.Log(1 + varianz / Math.Pow(mittlereDauer, 2));
                double sigma = Math.Sqrt(Math.Log(1 + varianz / Math.Pow(mittlereDauer, 2)));

                double dauer = MathNet.Numerics.Distributions.LogNormal.Sample(rnd, mu, sigma);
                daten.ErfasseRezeptionBehandlungszeit(dauer, hatTermin);

                yield return env.Timeout(TimeSpan.FromMinutes(dauer));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_rezeption", patientId);
                if (behandlungBereitsFertig)
                {
                    daten.LogEvent(nowMinutes, "macht_folgetermin_aus_oder_rezept", patientId);
                }
                else
                {
                    daten.LogEvent(nowMinutes, hatTermin ? "rezeption_hat_termin" : "rezeption_ohne_termin", patientId);
                }
            }
        }

        private static IEnumerable<Event> VerschiebeWegenRezeptionAufFolgetag(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            bool hatTermin,
            TimeSpan interneBewegungsdauer,
            BehandlungsPhaseErgebnis ergebnis)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.ErfasseRezeptionVerschobenSchlussplanung(env.StartDate);
            string eventTyp = hatTermin
                ? "erhaelt_festen_termin_am_naechsten_vormittag_rezeption"
                : "wird_auf_naechsten_tag_verschoben_rezeption";
            daten.LogEvent(nowMinutes, eventTyp, patientId);

            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(interneBewegungsdauer);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            ergebnis.MarkiereKlinikVerlassen();
        }

        private static bool IstRezeptionFrei(Resource rezeption)
        {
            var usersProperty = rezeption.GetType().GetProperty("Users", BindingFlags.NonPublic | BindingFlags.Instance);
            var usersCollection = usersProperty?.GetValue(rezeption) as IReadOnlyCollection<Request>;
            int aktiveNutzer = usersCollection?.Count ?? 0;
            return aktiveNutzer < RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN;
        }
    }
}
