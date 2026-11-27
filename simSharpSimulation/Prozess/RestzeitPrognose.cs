using System;

namespace simSharpSimulation
{
    internal static class RestzeitPrognose
    {
        // Restzeit-Schaetzung direkt nach der Rezeption.
        // Falls keine Schwester-Vorbereitung mehr noetig ist, ist dieser Teil 0.
        // Falls Vorbereitung noetig ist, schaetzen wir:
        // - ggf. Bewegung und Warten bis zur Schwester
        // - Schwesterbehandlung mit der vorab gezogenen Dauer
        internal static double BerechneSchwesterRestzeitNachRezeption(
            bool brauchtVorbereitung,
            bool direktZurSchwester,
            TimeSpan interneBewegungsdauer,
            double schwesterBehandlungsdauer,
            double schwesterWartezimmerdauer)
        {
            if (!brauchtVorbereitung)
            {
                return 0.0;
            }

            double restzeit = interneBewegungsdauer.TotalMinutes + schwesterBehandlungsdauer;
            if (!direktZurSchwester)
            {
                restzeit += interneBewegungsdauer.TotalMinutes + schwesterWartezimmerdauer;
            }

            return restzeit;
        }

        // Restzeit ab abgeschlossenem Schwesterpfad:
        // Bewegung zum Arzt-Wartezimmer + erwartete Wartezimmerzeit + Arztbehandlung.
        internal static double BerechneRestzeitAbSchwester(
            TimeSpan interneBewegungsdauer,
            double arztBehandlungsdauer,
            double arztWartezimmerdauer)
        {
            return (2.0 * interneBewegungsdauer.TotalMinutes) +
                   arztWartezimmerdauer +
                   arztBehandlungsdauer;
        }

        // Restzeit nach dem Arzt, wenn der konkrete Pfad bereits feststeht.
        // Dann brauchen wir keinen Erwartungswert mehr, sondern die direkte Pfadlänge.
        internal static double BerechneRestzeitNachArztMitKonkretemPfad(
            bool gehtNachArztZurRezeption,
            TimeSpan interneBewegungsdauer,
            TimeSpan rezeptionZumAusgangDauer,
            TimeSpan arztZumAusgangDauer,
            double rezeptionsdauerNachArzt)
        {
            if (!gehtNachArztZurRezeption)
            {
                return arztZumAusgangDauer.TotalMinutes;
            }

            return interneBewegungsdauer.TotalMinutes +
                   rezeptionsdauerNachArzt +
                   rezeptionZumAusgangDauer.TotalMinutes;
        }
    }
}
