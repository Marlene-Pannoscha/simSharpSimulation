using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace simSharpSimulation
{
    internal sealed partial class PrognoseStatistik
    {
        public string ErzeugePrognoseReportText()
        {
            var culture = CultureInfo.GetCultureInfo("de-DE");
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("Prognosemodell-Auswertung");
            sb.AppendLine(new string('=', 50));
            sb.AppendLine($"Prognoseprüfungen gesamt: {AnzahlPrognosePruefungen.ToString("N0", culture)}");
            sb.AppendLine(
                $"Richtig (+/-{(PrognoseTrefferToleranzAnteil * 100.0).ToString("N0", culture)} % " +
                $"oder +/-{PrognoseTrefferMindestToleranzMinuten.ToString("N0", culture)} min): {AnzahlPrognoseRichtig.ToString("N0", culture)}");
            sb.AppendLine($"Trefferquote: {PrognoseTrefferquote.ToString("N2", culture)} %");
            sb.AppendLine($"Prognose-Abbrüche: {AnzahlPrognoseAbbruch.ToString("N0", culture)}");
            sb.AppendLine();

            var nachPhase = prognoseErgebnisse
                .GroupBy(e => e.Phase)
                .OrderBy(g => g.Key)
                .ToList();

            if (nachPhase.Count > 0)
            {
                sb.AppendLine("Trefferquote je Phase");
                sb.AppendLine(new string('-', 50));
                foreach (var gruppe in nachPhase)
                {
                    int total = gruppe.Count();
                    int korrekt = gruppe.Count(e => e.Korrekt);
                    double quote = total > 0 ? (korrekt / (double)total) * 100.0 : 0.0;
                    double mittlerePrognose = gruppe.Average(e => e.PrognoseRestMinuten);
                    double mittlereIstRestzeit = gruppe.Average(e => e.IstRestMinuten);
                    double mittlereAbsoluteAbweichung = gruppe.Average(e => Math.Abs(e.AbweichungMinuten));
                    var gruppeMitBearbeitung = gruppe
                        .Where(e => e.GesampelteBearbeitungsRestMinuten.HasValue)
                        .ToList();
                    sb.AppendLine(
                        $"{gruppe.Key}: {korrekt}/{total} ({quote.ToString("N2", culture)} %) " +
                        $"| Prognose {mittlerePrognose.ToString("N2", culture)} min " +
                        $"| Ist {mittlereIstRestzeit.ToString("N2", culture)} min " +
                        $"| abs. Abw. {mittlereAbsoluteAbweichung.ToString("N2", culture)} min");
                    if (gruppeMitBearbeitung.Count > 0)
                    {
                        double mittlereBearbeitungsPrognose =
                            gruppeMitBearbeitung.Average(e => e.PrognoseBearbeitungsRestMinuten);
                        double mittlereBearbeitung =
                            gruppeMitBearbeitung.Average(e => e.GesampelteBearbeitungsRestMinuten!.Value);
                        double mittlereAbsoluteBearbeitungsAbweichung =
                            gruppeMitBearbeitung.Average(e => Math.Abs(e.AbweichungBearbeitungMinuten!.Value));
                        sb.AppendLine(
                            $"  Bearbeitung: Prognose {mittlereBearbeitungsPrognose.ToString("N2", culture)} min " +
                            $"| Sample {mittlereBearbeitung.ToString("N2", culture)} min " +
                            $"| abs. Abw. {mittlereAbsoluteBearbeitungsAbweichung.ToString("N2", culture)} min");
                    }
                }
            }

            return sb.ToString();
        }

        public void SchreibePrognoseReport(string dateiPfad)
        {
            string report = ErzeugePrognoseReportText();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(report);
            sb.AppendLine();
            sb.AppendLine("Details");
            sb.AppendLine("PatientId;Phase;ZeitpunktMin;PrognoseRestMin;IstRestMin;AbweichungMin;AbweichungProzent;PrognoseBearbeitungsRestMin;GesampelteBearbeitungsRestMin;AbweichungBearbeitungMin;AbweichungBearbeitungProzent;Korrekt;PrognoseFertigBisSchichtende");
            foreach (var eintrag in prognoseErgebnisse)
            {
                sb.AppendLine(string.Join(";", new[]
                {
                    eintrag.PatientId.ToString(CultureInfo.InvariantCulture),
                    eintrag.Phase,
                    eintrag.ZeitpunktMinuten.ToString("F2", CultureInfo.InvariantCulture),
                    eintrag.PrognoseRestMinuten.ToString("F2", CultureInfo.InvariantCulture),
                    eintrag.IstRestMinuten.ToString("F2", CultureInfo.InvariantCulture),
                    eintrag.AbweichungMinuten.ToString("F2", CultureInfo.InvariantCulture),
                    eintrag.AbweichungProzent?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                    eintrag.PrognoseBearbeitungsRestMinuten.ToString("F2", CultureInfo.InvariantCulture),
                    eintrag.GesampelteBearbeitungsRestMinuten?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                    eintrag.AbweichungBearbeitungMinuten?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                    eintrag.AbweichungBearbeitungProzent?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                    eintrag.Korrekt ? "1" : "0",
                    eintrag.PrognoseFertigBisSchichtende ? "1" : "0"
                }));
            }

            File.WriteAllText(dateiPfad, sb.ToString());
        }

        public void SchreibePrognoseDatenJson(string dateiPfad, int rezeptionFeierabend,
            int schwesterFeierabend, int arztFeierabend)
        {
            var phaseDaten = prognoseErgebnisse
                .GroupBy(e => e.Phase)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Phase = g.Key,
                    Anzahl = g.Count(),
                    Korrekt = g.Count(e => e.Korrekt),
                    Trefferquote = g.Any() ? (g.Count(e => e.Korrekt) / (double)g.Count()) * 100.0 : 0.0
                })
                .ToList();

            var daten = new
            {
                AnzahlPrognosePruefungen,
                AnzahlPrognoseRichtig,
                AnzahlPrognoseAbbruch,
                PrognoseTrefferToleranzProzent = PrognoseTrefferToleranzAnteil * 100.0,
                PrognoseTrefferMindestToleranzMinuten,
                PrognoseTrefferquote,
                Abbruchgruende = new
                {
                    Prognose = AnzahlPrognoseAbbruch,
                    Aufnahmeprognose = AnzahlPrognoseAufnahmeAbgewiesen,
                    RezeptionFeierabend = rezeptionFeierabend,
                    SchwesterFeierabend = schwesterFeierabend,
                    ArztFeierabend = arztFeierabend
                },
                Phasen = phaseDaten,
                Ergebnisse = prognoseErgebnisse.Select(e => new
                {
                    e.PatientId,
                    e.Phase,
                    e.ZeitpunktMinuten,
                    e.PrognoseRestMinuten,
                    IstRestMinuten = e.IstRestMinuten,
                    e.AbweichungMinuten,
                    e.AbweichungProzent,
                    e.PrognoseBearbeitungsRestMinuten,
                    e.GesampelteBearbeitungsRestMinuten,
                    e.AbweichungBearbeitungMinuten,
                    e.AbweichungBearbeitungProzent,
                    e.Korrekt,
                    e.PrognoseFertigBisSchichtende
                }),
                PrognoseAbbrueche = prognoseAbbrueche.Select(a => new
                {
                    a.ZeitpunktMinuten,
                    a.Phase
                }),
                AufnahmeprognosePruefungen = prognoseAufnahmePruefungen.Select(p => new
                {
                    p.Tag,
                    p.ZeitpunktMinuten,
                    p.AufnahmeKapazitaet
                }),
                AufnahmeprognoseEntscheidungen = prognoseAufnahmeEntscheidungen.Select(e => new
                {
                    e.Tag,
                    e.ZeitpunktMinuten,
                    e.PatientId,
                    e.Zugelassen,
                    e.RestKapazitaet
                })
            };

            var optionen = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(dateiPfad, JsonSerializer.Serialize(daten, optionen));
        }
    }
}
