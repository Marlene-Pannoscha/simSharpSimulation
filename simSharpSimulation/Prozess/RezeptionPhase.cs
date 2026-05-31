using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik fuer die Rezeptions-Phase.
     * Die Warteschlange ist analog zu Arzt und Schwester begrenzt:
     * warten auf freie Ressource oder Abbruch wegen Wartezeit/Feierabend.
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
                foreach (Event ev in BrichRezeptionWartenAb(env, daten, patientId, interneBewegungsdauer, wegenFeierabend: true, ergebnis))
                    yield return ev;
                yield break;
            }

            while (rezeption.Remaining <= 0)
            {
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double restMinuten = schichtEndeMinuten - nowMinutes;
                if (restMinuten <= 0)
                {
                    foreach (Event ev in BrichRezeptionWartenAb(env, daten, patientId, interneBewegungsdauer, wegenFeierabend: true, ergebnis))
                        yield return ev;
                    yield break;
                }

                Event rezeptionVerfuegbar = rezeption.WhenAny();
                Event schichtEnde = env.Timeout(TimeSpan.FromMinutes(restMinuten));
                yield return rezeptionVerfuegbar | schichtEnde;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                if (!rezeptionVerfuegbar.IsProcessed)
                {
                    foreach (Event ev in BrichRezeptionWartenAb(env, daten, patientId, interneBewegungsdauer, wegenFeierabend: true, ergebnis))
                        yield return ev;
                    yield break;
                }
            }

            using (Request req = rezeption.Request())
            {
                yield return req;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                if (nowMinutes > schichtEndeMinuten)
                {
                    foreach (Event ev in BrichRezeptionWartenAb(env, daten, patientId, interneBewegungsdauer, wegenFeierabend: true, ergebnis))
                        yield return ev;
                    yield break;
                }

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
            // Die Ressource wird hier durch 'using' automatisch freigegeben.
        }

        private static bool IstRezeptionFrei(Resource rezeption)
        {
            return rezeption.Remaining > 0;
        }
    }
}
