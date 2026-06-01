using System.Drawing;
using System.Globalization;
using System.Text;
using System.IO;
using MathNet.Numerics.Distributions;
using ScottPlot;

namespace simSharpSimulation;

internal static class FinanzVisualisierung
{
    // Deutsche Kultur fuer gut lesbare EUR- und Zahlenformate in UI und Exporten.
    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

    public static readonly string[] ZeitraumOptionen = { "Jahr", "Winter", "Fruehling", "Sommer", "Herbst" };

    public static FinanzErgebnis Simuliere(int anzahlAerzte, int anzahlSchwestern, string zeitraum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlAerzte, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlSchwestern, 1);

        // Im Jahresmodus werden Tageswerte spaeter zu Monatswerten verdichtet.
        string normalisierterZeitraum = NormalisiereZeitraum(zeitraum);
        bool aggregiereNachMonat = string.Equals(normalisierterZeitraum, "Jahr", StringComparison.OrdinalIgnoreCase);

        Random rnd = new(42);
        List<int> tage = GetDayNumbersForPeriod(normalisierterZeitraum);
        Dictionary<string, FinanzAggregat> aggregierteWerte = new();
        List<FinanzTagespunkt> tagespunkte = new();

        Dictionary<string, double> saisonGewinn = new()
        {
            ["Winter"] = 0.0,
            ["Fruehling"] = 0.0,
            ["Sommer"] = 0.0,
            ["Herbst"] = 0.0,
        };

        int gesamteNachfrage = 0;
        int gesamtBehandelt = 0;

        // Keep the raw daily Tagesergebnisse so we can correctly aggregate them when converting to months.
        List<Tagesergebnis> alleTagesergebnisse = new();

        // Jeder Tag wird separat simuliert und danach je nach Zeitraum direkt gespeichert oder aggregiert.
        for (int index = 0; index < tage.Count; index++)
        {
            int tagImJahr = tage[index];
            string saison = GetSeasonFromDay(tagImJahr);

            int nachfrage = SimuliereTaeglicheNachfrage(tagImJahr, rnd);
            int behandeltePatientenTag = BerechneBehandeltePatienten(anzahlAerzte, anzahlSchwestern, nachfrage, rnd);
            Tagesergebnis tagesergebnis = FinanzRechner.BerechneTagesergebnis(
                anzahlAerzte,
                anzahlSchwestern,
                RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN,
                behandeltePatientenTag);

            // store the raw daily result for later correct aggregation
            alleTagesergebnisse.Add(tagesergebnis);

            FinanzTagespunkt tagespunkt = new(
                (index + 1).ToString(CultureInfo.InvariantCulture),
                behandeltePatientenTag,
                tagesergebnis.Umsatz,
                tagesergebnis.Kosten.Gesamtkosten,
                tagesergebnis.Gewinn,
                tagesergebnis.Versicherungen,
                tagesergebnis.Behandlungsmix);

            if (aggregiereNachMonat)
            {
                string monat = GetMonthFromDay(tagImJahr);
                if (!aggregierteWerte.TryGetValue(monat, out FinanzAggregat? aggregat))
                {
                    aggregat = new FinanzAggregat();
                    aggregierteWerte[monat] = aggregat;
                }

                aggregat.Add(tagespunkt);
            }
            else
            {
                tagespunkte.Add(tagespunkt);
            }

            saisonGewinn[saison] += tagesergebnis.Gewinn;
            gesamteNachfrage += nachfrage;
            gesamtBehandelt += behandeltePatientenTag;
        }

        if (aggregiereNachMonat)
        {
            // Fuer die Diagramme bleibt die Monatsreihenfolge auch dann stabil, wenn ein Monat leer ist.
            string[] monate =
            {
                "Januar", "Februar", "Maerz", "April", "Mai", "Juni",
                "Juli", "August", "September", "Oktober", "November", "Dezember"
            };

            foreach (string monat in monate)
            {
                FinanzAggregat aggregat = aggregierteWerte.GetValueOrDefault(monat) ?? new FinanzAggregat();
                tagespunkte.Add(aggregat.ToTagespunkt(monat));
            }
        }

        double gesamtgewinn = tagespunkte.Sum(p => p.Gewinn);
        int intervalle = tagespunkte.Count;
        double durchschnittGewinn = intervalle > 0 ? gesamtgewinn / intervalle : 0.0;

        var fixkosten = KonfigurationJsonExport.Finanzen.Fixkosten;
        double gesamtflaeche = fixkosten.AnzahlBehandlungsraeumeSchwester * fixkosten.FlaecheBehandlungsraumSchwesterQuadratmeter
                             + fixkosten.AnzahlBehandlungsraeumeArzt * fixkosten.FlaecheBehandlungsraumArztQuadratmeter
                             + fixkosten.FlaecheWartezimmerQuadratmeter;

        double mietkostenProQm = FinanzRechner.GetMietkostenProQuadratmeterProMonat(gesamtflaeche);
        double gesamtMietkostenMonat = mietkostenProQm * gesamtflaeche;
        double gesamtMietkostenProTag = (gesamtMietkostenMonat * 12) / 365.0;
        double gesamtkostenFix = gesamtMietkostenProTag + fixkosten.InfrastrukturProTag + fixkosten.MedizinischesMaterialProTag + fixkosten.GeraeteLeasingProTag + fixkosten.SonstigeFixkostenProTag;

        List<Tagesergebnis> tagesergebnisse;
        if (aggregiereNachMonat)
        {
            // Aggregate the raw daily Tagesergebnisse into monthly Gesamtwerte so cost components are summed correctly.
            string[] monate =
            {
                "Januar", "Februar", "Maerz", "April", "Mai", "Juni",
                "Juli", "August", "September", "Oktober", "November", "Dezember"
            };

            tagesergebnisse = new List<Tagesergebnis>(monate.Length);
            for (int m = 0; m < monate.Length; m++)
            {
                string monat = monate[m];
                // find indices of days that belong to this month
                var indices = Enumerable.Range(0, tage.Count).Where(i => GetMonthFromDay(tage[i]) == monat).ToList();

                if (!indices.Any())
                {
                    // empty month -> zeroed result
                    Tageskosten zeroKosten = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
                    tagesergebnisse.Add(new Tagesergebnis(0, 0, zeroKosten, new Versicherungsverteilung(0, 0), new Umsatzverteilung(0, 0), new Behandlungsmix(0, 0, 0, 0, 0, 0), new Kostenstruktur(), new BreakEvenPoint()));
                    continue;
                }

                double sumUmsatz = 0.0;
                double sumGewinn = 0.0;
                double sumArzt = 0.0;
                double sumSchwester = 0.0;
                double sumRezeption = 0.0;
                double sumMiete = 0.0;
                double sumInfrastruktur = 0.0;
                double sumMaterial = 0.0;
                double sumLeasing = 0.0;
                double sumSonstige = 0.0;
                double sumBehandlungskosten = 0.0;
                int sumPrivat = 0;
                int sumGesetzlich = 0;
                double sumUmsatzPrivat = 0.0;
                double sumUmsatzGesetzlich = 0.0;
                int sumKurz = 0; int sumMittel = 0; int sumLang = 0;
                double sumKurzKosten = 0; double sumMittelKosten = 0; double sumLangKosten = 0;

                foreach (int idx in indices)
                {
                    var dr = alleTagesergebnisse[idx];
                    sumUmsatz += dr.Umsatz;
                    sumGewinn += dr.Gewinn;
                    sumArzt += dr.Kosten.Arztlohn;
                    sumSchwester += dr.Kosten.Schwesterlohn;
                    sumRezeption += dr.Kosten.Rezeptionlohn;
                    sumMiete += dr.Kosten.Mietkosten;
                    sumInfrastruktur += dr.Kosten.Infrastrukturkosten;
                    sumMaterial += dr.Kosten.MedizinischesMaterialkosten;
                    sumLeasing += dr.Kosten.GeraeteLeasingKosten;
                    sumSonstige += dr.Kosten.SonstigeFixkosten;
                    sumBehandlungskosten += dr.Kosten.Behandlungskosten;
                    sumPrivat += dr.Versicherungen.PrivatPatienten;
                    sumGesetzlich += dr.Versicherungen.GesetzlichPatienten;
                    sumUmsatzPrivat += dr.Umsatzverteilung.UmsatzPrivat;
                    sumUmsatzGesetzlich += dr.Umsatzverteilung.UmsatzGesetzlich;
                    sumKurz += dr.Behandlungsmix.KurzPatienten;
                    sumMittel += dr.Behandlungsmix.MittelPatienten;
                    sumLang += dr.Behandlungsmix.LangPatienten;
                    sumKurzKosten += dr.Behandlungsmix.KurzKosten;
                    sumMittelKosten += dr.Behandlungsmix.MittelKosten;
                    sumLangKosten += dr.Behandlungsmix.LangKosten;
                }

                Tageskosten gesamtKosten = new(
                    sumArzt,
                    sumSchwester,
                    sumRezeption,
                    sumMiete,
                    sumInfrastruktur,
                    sumMaterial,
                    sumLeasing,
                    sumSonstige,
                    sumBehandlungskosten);

                Versicherungsverteilung vers = new(sumPrivat, sumGesetzlich);
                Umsatzverteilung umv = new(sumUmsatzPrivat, sumUmsatzGesetzlich);
                Behandlungsmix bm = new(sumKurz, sumMittel, sumLang, sumKurzKosten, sumMittelKosten, sumLangKosten);
                // Berechne Kostenstruktur-Anteile manuell (FinanzRechner.BerechneKostenstruktur ist nicht öffentlich)
                double sumPersonalkosten = sumArzt + sumSchwester + sumRezeption;
                Kostenstruktur ks = new()
                {
                    PersonalkostenAnteil = sumUmsatz > 0 ? sumPersonalkosten / sumUmsatz : 0.0,
                    MietkostenAnteil = sumUmsatz > 0 ? sumMiete / sumUmsatz : 0.0,
                    InfrastrukturkostenAnteil = sumUmsatz > 0 ? sumInfrastruktur / sumUmsatz : 0.0,
                    MaterialkostenAnteil = sumUmsatz > 0 ? sumMaterial / sumUmsatz : 0.0,
                    GeraeteLeasingAnteil = sumUmsatz > 0 ? sumLeasing / sumUmsatz : 0.0,
                    SonstigeFixkostenAnteil = sumUmsatz > 0 ? sumSonstige / sumUmsatz : 0.0,
                    BehandlungskostenAnteil = sumUmsatz > 0 ? sumBehandlungskosten / sumUmsatz : 0.0
                };

                tagesergebnisse.Add(new Tagesergebnis(sumUmsatz, sumGewinn, gesamtKosten, vers, umv, bm, ks, new BreakEvenPoint()));
            }
        }
        else
        {
            tagesergebnisse = new List<Tagesergebnis>(alleTagesergebnisse);
        }

        return new FinanzErgebnis(
            normalisierterZeitraum,
            tage.Count,
            tagespunkte,
            tagesergebnisse,
            gesamteNachfrage,
            gesamtBehandelt,
            gesamtgewinn,
            durchschnittGewinn,
            saisonGewinn,
            gesamtflaeche,
            mietkostenProQm,
            gesamtMietkostenProTag,
            gesamtkostenFix);
    }

    public static (string PatientenPfad, string GewinnPfad, string KostenstrukturPfad) ErzeugeDiagramme(
        FinanzErgebnis ergebnis,
        int anzahlAerzte,
        int anzahlSchwestern)
    {
        string outputOrdner = ErzeugeKostenImageOrdner();
        string zeitraumSlug = SanitizeDateiname(ergebnis.Zeitraum.ToLowerInvariant());

        string finanzenPfad = Path.Combine(outputOrdner, $"finanzen_{zeitraumSlug}.png");
        string gewinnPfad = Path.Combine(outputOrdner, $"gewinn_{zeitraumSlug}.png");
        string kostenstrukturPfad = Path.Combine(outputOrdner, $"kostenstruktur_{zeitraumSlug}.png");

        ErzeugeFinanzenDiagramm(ergebnis, anzahlAerzte, anzahlSchwestern, finanzenPfad);
        ErzeugeGewinnDiagramm(ergebnis, anzahlAerzte, anzahlSchwestern, gewinnPfad);
        ErzeugeKostenstrukturDiagramm(ergebnis, kostenstrukturPfad);

        return (finanzenPfad, gewinnPfad, kostenstrukturPfad);
    }

    public static (int AnzahlHit, int AnzahlMiss, string DiagrammPfad) ErzeugeHitMissDiagramm(FinanzErgebnis ergebnis)
    {
        string outputOrdner = ErzeugeKostenImageOrdner();
        string zeitraumSlug = SanitizeDateiname(ergebnis.Zeitraum.ToLowerInvariant());
        string hitMissPfad = Path.Combine(outputOrdner, $"behandelte_patienten_{zeitraumSlug}.png");

        int anzahlHit = ergebnis.GesamtBehandelt;
        int anzahlMiss = ergebnis.GesamtNichtBehandelt;

        ErzeugeHitMissDiagrammIntern(anzahlHit, anzahlMiss, ergebnis.Zeitraum, hitMissPfad);
        return (anzahlHit, anzahlMiss, hitMissPfad);
    }

    public static string FormatEuro(double wert) => string.Format(DeCulture, "{0:N2} EUR", wert);

    public static string FormatBreakEven(BreakEvenPoint breakEven, double aktuellePatientenProTag)
    {
        if (breakEven.Patienten == int.MaxValue || double.IsInfinity(breakEven.Tage) || double.IsNaN(breakEven.Tage))
            return "Break-Even: nicht erreichbar";

        double differenz = aktuellePatientenProTag - breakEven.Patienten;
        string differenzText = differenz >= 0
            ? $"+{differenz.ToString("N1", DeCulture)} Patienten/Tag"
            : $"{differenz.ToString("N1", DeCulture)} Patienten/Tag";

        return $"Break-Even: {breakEven.Patienten} Patienten/Tag ({breakEven.Tage.ToString("N2", DeCulture)} Tage bei aktueller Auslastung, Differenz {differenzText})";
    }

    // Generiert den gleichen Textbericht wie die WPF-Ansicht (für Konsolen-Ausgabe oder Tests).
    public static string GenerateErgebnisReportText(FinanzErgebnis ergebnis, string finanzenPfad, string gewinnPfad, string kostenstrukturPfad)
    {
        // Reuse the formatting and labels used in the WPF view so console and WPF are identical.
        StringBuilder sb = new();
        sb.AppendLine("Ergebnis");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"Zeitraum: {ergebnis.Zeitraum}");
        sb.AppendLine($"Simulierte Tage: {ergebnis.SimulierteTage}");
        sb.AppendLine($"Gesamtumsatz: {FormatEuro(ergebnis.GesamtUmsatz)}");
        sb.AppendLine($"Gesamtkosten: {FormatEuro(ergebnis.Gesamtkosten)}");
        sb.AppendLine($"Gesamtgewinn: {FormatEuro(ergebnis.Gesamtgewinn)}");
        sb.AppendLine($"Durchschnitt Umsatz pro Tag: {FormatEuro(ergebnis.DurchschnittlicherUmsatzProTag)}");
        sb.AppendLine($"Durchschnitt Kosten pro Tag: {FormatEuro(ergebnis.DurchschnittlicheKostenProTag)}");
        sb.AppendLine($"Durchschnitt Gewinn pro {ergebnis.DurchschnittLabel}: {FormatEuro(ergebnis.DurchschnittlicherGewinnProEinheit)}");
        sb.AppendLine($"Durchschnitt behandelte Patienten pro Tag: {ergebnis.DurchschnittBehandeltePatientenProTag.ToString("N1", DeCulture)}");
        sb.AppendLine(FormatBreakEven(ergebnis.BreakEven, ergebnis.DurchschnittBehandeltePatientenProTag));
        sb.AppendLine();
        sb.AppendLine("Praxisdetails");
        sb.AppendLine($"Gesamtfläche: {ergebnis.Gesamtflaeche.ToString("N2", DeCulture)} m²");
        sb.AppendLine($"Mietkosten pro m²/Monat: {FormatEuro(ergebnis.MietkostenProQm)}");
        sb.AppendLine($"Gesamtmietkosten pro Tag: {FormatEuro(ergebnis.GesamtMietkostenProTag)}");
        double gesamtMietkostenMonat = ergebnis.MietkostenProQm * ergebnis.Gesamtflaeche;
        sb.AppendLine($"Gesamtmietkosten pro Monat: {FormatEuro(gesamtMietkostenMonat)}");
        sb.AppendLine($"Gesamtmietkosten pro Jahr: {FormatEuro(gesamtMietkostenMonat * 12)}");
        sb.AppendLine();
        sb.AppendLine("Kostenstruktur (Basis: Umsatz)");
        sb.AppendLine($"Personalkosten: {FormatEuro(ergebnis.GesamtPersonalkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtPersonalkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Mietkosten (im Zeitraum, gesamt): {FormatEuro(ergebnis.GesamtMietkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtMietkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Infrastruktur: {FormatEuro(ergebnis.GesamtInfrastrukturkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtInfrastrukturkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Medizinisches Material: {FormatEuro(ergebnis.GesamtMaterialkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtMaterialkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Geräte-Leasing: {FormatEuro(ergebnis.GesamtLeasingkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtLeasingkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Sonstige Fixkosten: {FormatEuro(ergebnis.GesamtSonstigeFixkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtSonstigeFixkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Behandlungskosten: {FormatEuro(ergebnis.GesamtBehandlungskosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtBehandlungskosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Gewinn: {FormatEuro(ergebnis.Gesamtgewinn)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.Gesamtgewinn / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine();
        sb.AppendLine("Saisonaler Gewinn (im gewählten Zeitraum)");
        foreach (string saison in new[] { "Winter", "Fruehling", "Sommer", "Herbst" })
        {
            double saisonWert = ergebnis.SaisonGewinn.TryGetValue(saison, out double wert) ? wert : 0.0;
            sb.AppendLine($"{saison}: {FormatEuro(saisonWert)}");
        }
        sb.AppendLine();
        sb.AppendLine("Versicherung");
        sb.AppendLine($"Privat: {ergebnis.VersicherungenGesamt.PrivatPatienten} Patienten / {FormatEuro(ergebnis.UmsatzverteilungGesamt.UmsatzPrivat)}");
        sb.AppendLine($"Gesetzlich: {ergebnis.VersicherungenGesamt.GesetzlichPatienten} Patienten / {FormatEuro(ergebnis.UmsatzverteilungGesamt.UmsatzGesetzlich)}");
        sb.AppendLine();
        sb.AppendLine("Behandlungsdauer");
        sb.AppendLine($"Kurz: {ergebnis.BehandlungsmixGesamt.KurzPatienten} Patienten / {FormatEuro(ergebnis.BehandlungsmixGesamt.KurzKosten)}");
        sb.AppendLine($"Mittel: {ergebnis.BehandlungsmixGesamt.MittelPatienten} Patienten / {FormatEuro(ergebnis.BehandlungsmixGesamt.MittelKosten)}");
        sb.AppendLine($"Lang: {ergebnis.BehandlungsmixGesamt.LangPatienten} Patienten / {FormatEuro(ergebnis.BehandlungsmixGesamt.LangKosten)}");
        sb.AppendLine($"Zusatzkosten Behandlungsdauer: {FormatEuro(ergebnis.BehandlungsmixGesamt.Gesamtkosten)}");
        sb.AppendLine();
        sb.AppendLine("Dateien");
        sb.AppendLine($"- Finanzen: {finanzenPfad}");
        sb.AppendLine($"- Gewinn: {gewinnPfad}");
        sb.AppendLine($"- Kostenstruktur: {kostenstrukturPfad}");
        return sb.ToString();
    }

    private static string NormalisiereZeitraum(string? zeitraum)
    {
        // Faengt freie oder leere Eingaben robust ab und nutzt dann den Standardwert.
        if (string.IsNullOrWhiteSpace(zeitraum))
            return "Jahr";

        return ZeitraumOptionen
            .FirstOrDefault(z => string.Equals(z, zeitraum.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? "Jahr";
    }

    public static List<int> GetDayNumbersForPeriod(string zeitraum)
    {
        return zeitraum switch
        {
            "Jahr" => Enumerable.Range(0, 365).ToList(),
            "Winter" => Enumerable.Range(334, 31).Concat(Enumerable.Range(0, 59)).ToList(),
            "Fruehling" => Enumerable.Range(59, 92).ToList(),
            "Sommer" => Enumerable.Range(151, 92).ToList(),
            "Herbst" => Enumerable.Range(243, 91).ToList(),
            _ => Enumerable.Range(0, 365).ToList(),
        };
    }

    public static string GetSeasonFromDay(int tagImJahr)
    {
        if (tagImJahr >= 334 || tagImJahr < 59)
            return "Winter";
        if (tagImJahr < 151)
            return "Fruehling";
        if (tagImJahr < 243)
            return "Sommer";
        return "Herbst";
    }

    public static string GetMonthFromDay(int tagImJahr)
    {
        (string Monat, int Start, int Ende)[] monate =
        {
            ("Januar", 0, 31),
            ("Februar", 31, 59),
            ("Maerz", 59, 90),
            ("April", 90, 120),
            ("Mai", 120, 151),
            ("Juni", 151, 181),
            ("Juli", 181, 212),
            ("August", 212, 243),
            ("September", 243, 273),
            ("Oktober", 273, 304),
            ("November", 304, 334),
            ("Dezember", 334, 365)
        };

        foreach ((string monat, int start, int ende) in monate)
        {
            if (tagImJahr >= start && tagImJahr < ende)
                return monat;
        }

        return "Dezember";
    }

    private static int SimuliereTaeglicheNachfrage(int tagImJahr, Random rnd)
    {
        string saison = GetSeasonFromDay(tagImJahr);
        var saisonfaktoren = KonfigurationJsonExport.Finanzen.Saisonfaktoren;
        // Saisonfaktoren verschieben die erwartete Nachfrage ueber das Jahr hinweg.
        double saisonfaktor = saison switch
        {
            "Winter" => saisonfaktoren.Winter,
            "Fruehling" => saisonfaktoren.Fruehling,
            "Sommer" => saisonfaktoren.Sommer,
            "Herbst" => saisonfaktoren.Herbst,
            _ => 1.0,
        };

        double basisNachfrage = PatientenKonfiguration.ANZAHL_PATIENTEN_TAG;
        double mean = basisNachfrage * saisonfaktor;
        double std = Math.Max(3.0, mean * 0.15);
        // Eine Normalverteilung erzeugt leichte Tagesschwankungen um den saisonalen Erwartungswert.
        int nachfrage = (int)Math.Round(Normal.Sample(rnd, mean, std));
        return Math.Max(0, nachfrage);
    }

    private static int BerechneBehandeltePatienten(
        int anzahlAerzte,
        int anzahlSchwestern,
        int nachfrage,
        Random rnd)
    {
        // Die Tageskapazitaet wird durch den engeren Engpass aus Arzt- und Schwesterzeit begrenzt.
        double erwartungswertArzt = PatientenKonfiguration.TYPEN_VERTEILUNG
            .Sum(t => t.Wahrscheinlichkeit * t.BehandlungszeitArzt);
        double erwartungswertSchwester = PatientenKonfiguration.TYPEN_VERTEILUNG
            .Sum(t => t.Wahrscheinlichkeit * t.BehandlungszeitSchwester);

        double arztKapazitaet = anzahlAerzte * (8.0 * 60.0 / Math.Max(erwartungswertArzt, 1.0));
        double schwesterKapazitaet = anzahlSchwestern * (8.0 * 60.0 / Math.Max(erwartungswertSchwester, 1.0));

        double brauchtSchwesterWahrscheinlichkeit =
            (PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT * PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT) +
            ((1.0 - PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT) * PatientenKonfiguration.OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT);

        brauchtSchwesterWahrscheinlichkeit = Math.Max(brauchtSchwesterWahrscheinlichkeit, 0.001);

        // Ein kleiner Tagesfaktor simuliert produktivere oder schwaechere Arbeitstage.
        double nurseAdjustedCapacity = schwesterKapazitaet / brauchtSchwesterWahrscheinlichkeit;
        double basisKapazitaet = Math.Min(arztKapazitaet, nurseAdjustedCapacity);
        double tagesfaktor = 0.90 + rnd.NextDouble() * 0.20;

        int kapazitaetHeute = (int)Math.Round(basisKapazitaet * tagesfaktor);
        return Math.Min(nachfrage, Math.Max(0, kapazitaetHeute));
    }

    private static void ErzeugeFinanzenDiagramm(
        FinanzErgebnis ergebnis,
        int anzahlAerzte,
        int anzahlSchwestern,
        string outputPfad)
    {
        // Das erste Diagramm stellt Umsatz und Kosten direkt gegenueber.
        Plot plot = new(1300, 600);
        double[] xs = Enumerable.Range(0, ergebnis.Tagespunkte.Count).Select(i => (double)i).ToArray();
        double[] umsatz = ergebnis.Tagespunkte.Select(p => p.Umsatz).ToArray();
        double[] kosten = ergebnis.Tagespunkte.Select(p => p.Kosten).ToArray();

        var umsatzLinie = plot.AddScatterLines(xs, umsatz, color: Color.SeaGreen);
        umsatzLinie.MarkerSize = 4;
        umsatzLinie.Label = "Umsatz";

        var kostenLinie = plot.AddScatterLines(xs, kosten, color: Color.OrangeRed);
        kostenLinie.MarkerSize = 4;
        kostenLinie.Label = "Kosten";

        plot.XTicks(xs, ergebnis.Achsenwerte.ToArray());
        plot.XAxis.TickLabelStyle(fontSize: 14, rotation: ergebnis.Achsenwerte.Count > 20 ? 45 : 0);
        plot.XAxis.LabelStyle(fontSize: 16);
        plot.YAxis.LabelStyle(fontSize: 16);
        plot.Title($"Umsatz und Kosten - Aerzte: {anzahlAerzte}, Schwestern: {anzahlSchwestern} - {ergebnis.Zeitraum}", size: 18);
        plot.Legend(location: Alignment.UpperRight);
        plot.Grid(enable: true, lineStyle: LineStyle.Dot);
        plot.SaveFig(outputPfad);
    }

    private static void ErzeugeGewinnDiagramm(
        FinanzErgebnis ergebnis,
        int anzahlAerzte,
        int anzahlSchwestern,
        string outputPfad)
    {
        // Positive und negative Gewinne werden getrennt eingefaerbt, damit Verlustphasen schneller auffallen.
        Plot plot = new(1300, 600);
        double[] xs = Enumerable.Range(0, ergebnis.GewinnVerlauf.Count).Select(i => (double)i).ToArray();
        double[] positive = ergebnis.GewinnVerlauf.Select(v => v > 0 ? v : 0).ToArray();
        double[] negative = ergebnis.GewinnVerlauf.Select(v => v < 0 ? v : 0).ToArray();

        var barsPos = plot.AddBar(positive, xs);
        barsPos.FillColor = Color.SeaGreen;
        barsPos.BorderColor = Color.DarkGreen;
        barsPos.Label = "Gewinn (>= 0)";

        var barsNeg = plot.AddBar(negative, xs);
        barsNeg.FillColor = Color.IndianRed;
        barsNeg.BorderColor = Color.DarkRed;
        barsNeg.Label = "Gewinn (< 0)";

        var nullLinie = plot.AddHorizontalLine(0, color: Color.Black);
        nullLinie.LineStyle = LineStyle.Dash;

        plot.XTicks(xs, ergebnis.Achsenwerte.ToArray());
        plot.XAxis.TickLabelStyle(fontSize: 14, rotation: ergebnis.Achsenwerte.Count > 20 ? 45 : 0);
        plot.XAxis.LabelStyle(fontSize: 16);
        plot.YAxis.LabelStyle(fontSize: 16);

        string titel = string.Equals(ergebnis.Zeitraum, "Jahr", StringComparison.OrdinalIgnoreCase)
            ? "Gewinn pro Monat"
            : "Gewinn pro Tag";

        plot.Title($"{titel} - Aerzte: {anzahlAerzte}, Schwestern: {anzahlSchwestern} - {ergebnis.Zeitraum}", size: 18);
        plot.Legend(location: Alignment.UpperRight);
        plot.Grid(enable: true, lineStyle: LineStyle.Dot);
        plot.SaveFig(outputPfad);
    }

    private static void ErzeugeHitMissDiagrammIntern(
        int anzahlHit,
        int anzahlMiss,
        string zeitraum,
        string outputPfad)
    {
        Plot plot = new(1000, 600);
        double[] positionen = { 0, 1 };
        string[] beschriftungen = { "Hit", "Miss" };

        var hitBalken = plot.AddBar(new[] { (double)anzahlHit }, new[] { 0.0 });
        hitBalken.FillColor = Color.SeaGreen;
        hitBalken.BorderColor = Color.Black;
        hitBalken.ShowValuesAboveBars = true;
        hitBalken.ValueFormatter = value => value.ToString("N0", DeCulture);

        var missBalken = plot.AddBar(new[] { (double)anzahlMiss }, new[] { 1.0 });
        missBalken.FillColor = Color.IndianRed;
        missBalken.BorderColor = Color.Black;
        missBalken.ShowValuesAboveBars = true;
        missBalken.ValueFormatter = value => value.ToString("N0", DeCulture);

        plot.XTicks(positionen, beschriftungen);
        plot.XAxis.TickLabelStyle(fontSize: 16);
        plot.YAxis.TickLabelFormat("N0", dateTimeFormat: false);
        plot.YAxis.Label("Patienten");
        plot.Title($"Hit/Miss Analyse - {zeitraum}", size: 18);
        plot.Grid(enable: true, lineStyle: LineStyle.Dot);
        plot.SetAxisLimits(yMin: 0);
        plot.SaveFig(outputPfad);
    }

    private static void ErzeugeKostenstrukturDiagramm(FinanzErgebnis ergebnis, string outputPfad)
    {
        // Etwas schmalere Grafik, damit sie besser in die WPF-Ansicht passt
        Plot plot = new(820, 620);

        double umsatz = ergebnis.GesamtUmsatz;
        double gewinn = ergebnis.Gesamtgewinn;
        bool mitGewinn = gewinn >= 0.0;
        double basis = mitGewinn ? umsatz : ergebnis.Gesamtkosten;
        string basisLabel = mitGewinn ? "Umsatz" : "Gesamtkosten";

        List<double> amounts = new()
        {
            ergebnis.GesamtPersonalkosten,
            ergebnis.GesamtMietkosten,
            ergebnis.GesamtInfrastrukturkosten,
            ergebnis.GesamtMaterialkosten,
            ergebnis.GesamtLeasingkosten,
            ergebnis.GesamtSonstigeFixkosten,
            ergebnis.GesamtBehandlungskosten
        };

        List<string> labels = new() { "Personal", "Miete", "Infrastruktur", "Material", "Leasing", "Sonstige", "Behandlung" };
        List<Color> farben = new()
        {
            Color.FromArgb(52, 152, 219),
            Color.FromArgb(155, 89, 182),
            Color.FromArgb(46, 204, 113),
            Color.FromArgb(241, 196, 15),
            Color.FromArgb(231, 76, 60),
            Color.FromArgb(149, 165, 166),
            Color.FromArgb(52, 73, 94)
        };

        if (mitGewinn)
        {
            amounts.Add(gewinn);
            labels.Add("Gewinn");
            farben.Add(Color.FromArgb(230, 126, 34));
        }

        bool hatWerte = amounts.Sum() > 0.0;
        if (!hatWerte)
        {
            amounts = new List<double> { 1.0 };
            labels = new List<string> { "Keine Werte" };
            farben = new List<Color> { Color.LightGray };
            basis = 0.0;
        }

        var pie = plot.AddPie(amounts.ToArray());
        // Disable ScottPlot-internal slice percentages; the legend uses the same basis as the slices.
        pie.SliceLabels = labels.ToArray();
        pie.ShowPercentages = false;
        pie.ShowLabels = false;
        pie.ShowValues = false;
        pie.SliceFillColors = farben.ToArray();

        // Leicht auseinanderziehen, damit Beschriftungen besser lesbar sind
        pie.Explode = true;

        // Labels/Prozente auf den Slices sind deaktiviert, damit das Diagramm ruhig und lesbar bleibt.
        pie.Size = 0.70;
        pie.SliceLabelPosition = 0.92;
        pie.SliceLabelColors = Enumerable.Repeat(Color.Black, amounts.Count).ToArray();

        string[] legendLabels = labels.Select((lab, i) =>
            $"{lab}: {FormatEuro(hatWerte ? amounts[i] : 0.0)} ({(basis > 0 ? amounts[i] / basis : 0.0):P2})").ToArray();
        pie.LegendLabels = legendLabels;

        string verlustHinweis = mitGewinn ? string.Empty : $", Verlust: {FormatEuro(Math.Abs(gewinn))}";
        plot.Title($"Kostenstruktur ({ergebnis.Zeitraum}, Basis: {basisLabel}{verlustHinweis})", size: 18);
        plot.Legend(location: Alignment.LowerRight);
        plot.SaveFig(outputPfad);
    }

    private static string ErzeugeKostenImageOrdner()
    {
        string projektOrdner = ErmittleProjektRoot();
        string outputOrdner = Path.Combine(projektOrdner, "Kosten", "images");
        // Der Zielordner wird bei Bedarf automatisch angelegt.
        Directory.CreateDirectory(outputOrdner);
        return outputOrdner;
    }

    private static string ErmittleProjektRoot()
    {
        string? viaCwd = FindeOrdnerMitDatei(Directory.GetCurrentDirectory(), "simSharpSimulation.csproj");
        if (!string.IsNullOrEmpty(viaCwd))
            return viaCwd;

        string? viaBase = FindeOrdnerMitDatei(AppContext.BaseDirectory, "simSharpSimulation.csproj");
        if (!string.IsNullOrEmpty(viaBase))
            return viaBase;

        return Directory.GetCurrentDirectory();
    }

    private static string? FindeOrdnerMitDatei(string startPfad, string dateiname)
    {
        // Laeuft vom Startordner nach oben, bis die Projektdatei gefunden wird.
        DirectoryInfo? current = new(startPfad);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, dateiname)))
                return current.FullName;
            current = current.Parent;
        }
        return null;
    }

    private static string SanitizeDateiname(string text)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            text = text.Replace(c, '_');
        return text;
    }

    private sealed class FinanzAggregat
    {
        // Sammelobjekt fuer Tageswerte, die im Jahresmodus monatsweise zusammengefasst werden.
        public int BehandeltePatienten { get; private set; }
        public double Umsatz { get; private set; }
        public double Kosten { get; private set; }
        public double Gewinn { get; private set; }
        public int PrivatPatienten { get; private set; }
        public int GesetzlichPatienten { get; private set; }
        public int KurzPatienten { get; private set; }
        public int MittelPatienten { get; private set; }
        public int LangPatienten { get; private set; }
        public double KurzKosten { get; private set; }
        public double MittelKosten { get; private set; }
        public double LangKosten { get; private set; }

        public void Add(FinanzTagespunkt punkt)
        {
            // Addiert alle relevanten Kennzahlen des Tages in das laufende Aggregat.
            BehandeltePatienten += punkt.BehandeltePatienten;
            Umsatz += punkt.Umsatz;
            Kosten += punkt.Kosten;
            Gewinn += punkt.Gewinn;
            PrivatPatienten += punkt.Versicherungen.PrivatPatienten;
            GesetzlichPatienten += punkt.Versicherungen.GesetzlichPatienten;
            KurzPatienten += punkt.Behandlungsmix.KurzPatienten;
            MittelPatienten += punkt.Behandlungsmix.MittelPatienten;
            LangPatienten += punkt.Behandlungsmix.LangPatienten;
            KurzKosten += punkt.Behandlungsmix.KurzKosten;
            MittelKosten += punkt.Behandlungsmix.MittelKosten;
            LangKosten += punkt.Behandlungsmix.LangKosten;
        }

        public FinanzTagespunkt ToTagespunkt(string label)
        {
            return new FinanzTagespunkt(
                label,
                BehandeltePatienten,
                Umsatz,
                Kosten,
                Gewinn,
                new Versicherungsverteilung(PrivatPatienten, GesetzlichPatienten),
                new Behandlungsmix(KurzPatienten, MittelPatienten, LangPatienten, KurzKosten, MittelKosten, LangKosten));
        }
    }
}

internal sealed record FinanzErgebnis(
    string Zeitraum,
    int SimulierteTage,
    IReadOnlyList<FinanzTagespunkt> Tagespunkte,
    IReadOnlyList<Tagesergebnis> Tagesergebnisse,
    int Gesamtnachfrage,
    int GesamtBehandelt,
    double Gesamtgewinn,
    double DurchschnittlicherGewinnProEinheit,
    IReadOnlyDictionary<string, double> SaisonGewinn,
    double Gesamtflaeche,
    double MietkostenProQm,
    double GesamtMietkostenProTag,
    double GesamtkostenFix)
{
    // Bequeme Projektionen fuer UI und Berichte, damit dort keine Summenlogik dupliziert wird.
    public IReadOnlyList<string> Achsenwerte => Tagespunkte.Select(p => p.Label).ToList();
    public IReadOnlyList<int> BehandeltePatienten => Tagespunkte.Select(p => p.BehandeltePatienten).ToList();
    public IReadOnlyList<double> GewinnVerlauf => Tagespunkte.Select(p => p.Gewinn).ToList();
    public double GesamtUmsatz => Tagespunkte.Sum(p => p.Umsatz);
    public double Gesamtkosten => Tagespunkte.Sum(p => p.Kosten);
    public double GesamtPersonalkosten => Tagesergebnisse.Sum(t => t.Kosten.Personalkosten);
    public double GesamtMietkosten => Tagesergebnisse.Sum(t => t.Kosten.Mietkosten);
    public double GesamtInfrastrukturkosten => Tagesergebnisse.Sum(t => t.Kosten.Infrastrukturkosten);
    public double GesamtMaterialkosten => Tagesergebnisse.Sum(t => t.Kosten.MedizinischesMaterialkosten);
    public double GesamtLeasingkosten => Tagesergebnisse.Sum(t => t.Kosten.GeraeteLeasingKosten);
    public double GesamtSonstigeFixkosten => Tagesergebnisse.Sum(t => t.Kosten.SonstigeFixkosten);
    public double GesamtBehandlungskosten => Tagesergebnisse.Sum(t => t.Kosten.Behandlungskosten);
    public double DurchschnittlicherUmsatzProTag => SimulierteTage > 0 ? GesamtUmsatz / SimulierteTage : 0.0;
    public double DurchschnittlicheKostenProTag => SimulierteTage > 0 ? Gesamtkosten / SimulierteTage : 0.0;
    public Tageskosten DurchschnittlicheTageskosten => SimulierteTage > 0
        ? new Tageskosten(
            Tagesergebnisse.Sum(t => t.Kosten.Arztlohn) / SimulierteTage,
            Tagesergebnisse.Sum(t => t.Kosten.Schwesterlohn) / SimulierteTage,
            Tagesergebnisse.Sum(t => t.Kosten.Rezeptionlohn) / SimulierteTage,
            GesamtMietkosten / SimulierteTage,
            GesamtInfrastrukturkosten / SimulierteTage,
            GesamtMaterialkosten / SimulierteTage,
            GesamtLeasingkosten / SimulierteTage,
            GesamtSonstigeFixkosten / SimulierteTage,
            GesamtBehandlungskosten / SimulierteTage)
        : new Tageskosten(0, 0, 0, 0, 0, 0, 0, 0, 0);
    public BreakEvenPoint BreakEven => FinanzRechner.BerechneBreakEvenPoint(
        DurchschnittlicheTageskosten,
        DurchschnittBehandeltePatientenProTag,
        GesamtBehandelt > 0 ? GesamtUmsatz / GesamtBehandelt : 0.0);
    public Versicherungsverteilung VersicherungenGesamt =>
        new(
            Tagespunkte.Sum(p => p.Versicherungen.PrivatPatienten),
            Tagespunkte.Sum(p => p.Versicherungen.GesetzlichPatienten));
    public Umsatzverteilung UmsatzverteilungGesamt =>
        new(
            Tagespunkte.Sum(p => p.Versicherungen.PrivatPatienten * KonfigurationJsonExport.Finanzen.Versicherung.EinnahmePrivatpatient),
            Tagespunkte.Sum(p => p.Versicherungen.GesetzlichPatienten * KonfigurationJsonExport.Finanzen.Versicherung.EinnahmeGesetzlichPatient));
    public Behandlungsmix BehandlungsmixGesamt =>
        new(
            Tagespunkte.Sum(p => p.Behandlungsmix.KurzPatienten),
            Tagespunkte.Sum(p => p.Behandlungsmix.MittelPatienten),
            Tagespunkte.Sum(p => p.Behandlungsmix.LangPatienten),
            Tagespunkte.Sum(p => p.Behandlungsmix.KurzKosten),
            Tagespunkte.Sum(p => p.Behandlungsmix.MittelKosten),
            Tagespunkte.Sum(p => p.Behandlungsmix.LangKosten));
    public int GesamtNichtBehandelt => Math.Max(0, Gesamtnachfrage - GesamtBehandelt);
    public double DurchschnittBehandeltePatientenProTag => SimulierteTage > 0 ? GesamtBehandelt / (double)SimulierteTage : 0.0;
    public double Behandlungsquote => Gesamtnachfrage > 0 ? (GesamtBehandelt / (double)Gesamtnachfrage) * 100.0 : 0.0;
    public string DurchschnittLabel => string.Equals(Zeitraum, "Jahr", StringComparison.OrdinalIgnoreCase) ? "Monat" : "Tag";
}

internal readonly record struct FinanzTagespunkt(
    string Label,
    int BehandeltePatienten,
    double Umsatz,
    double Kosten,
    double Gewinn,
    Versicherungsverteilung Versicherungen,
    Behandlungsmix Behandlungsmix);
