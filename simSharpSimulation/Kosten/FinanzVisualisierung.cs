using System.Drawing;
using System.Globalization;
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

        // Jeder Tag wird separat simuliert und danach je nach Zeitraum direkt gespeichert oder aggregiert.
        for (int index = 0; index < tage.Count; index++)
        {
            int tagImJahr = tage[index];
            string saison = GetSeasonFromDay(tagImJahr);

            int nachfrage = SimuliereTaeglicheNachfrage(tagImJahr, rnd);
            int behandeltePatientenTag = BerechneBehandeltePatienten(anzahlAerzte, anzahlSchwestern, nachfrage, rnd);
            Tagesergebnis tagesergebnis = FinanzRechner.BerechneTagesergebnis(anzahlAerzte, behandeltePatientenTag);

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

        return new FinanzErgebnis(
            normalisierterZeitraum,
            tage.Count,
            tagespunkte,
            gesamteNachfrage,
            gesamtBehandelt,
            gesamtgewinn,
            durchschnittGewinn,
            saisonGewinn);
    }

    public static (string PatientenPfad, string GewinnPfad) ErzeugeDiagramme(
        FinanzErgebnis ergebnis,
        int anzahlAerzte,
        int anzahlSchwestern)
    {
        string outputOrdner = ErzeugeKostenImageOrdner();
        string zeitraumSlug = SanitizeDateiname(ergebnis.Zeitraum.ToLowerInvariant());

        string finanzenPfad = Path.Combine(outputOrdner, $"finanzen_{zeitraumSlug}.png");
        string gewinnPfad = Path.Combine(outputOrdner, $"gewinn_{zeitraumSlug}.png");

        ErzeugeFinanzenDiagramm(ergebnis, anzahlAerzte, anzahlSchwestern, finanzenPfad);
        ErzeugeGewinnDiagramm(ergebnis, anzahlAerzte, anzahlSchwestern, gewinnPfad);

        return (finanzenPfad, gewinnPfad);
    }

    public static string FormatEuro(double wert) => string.Format(DeCulture, "{0:N2} EUR", wert);

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
        // Saisonfaktoren verschieben die erwartete Nachfrage ueber das Jahr hinweg.
        double saisonfaktor = saison switch
        {
            "Winter" => 1.15,
            "Fruehling" => 1.00,
            "Sommer" => 0.90,
            "Herbst" => 1.05,
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
        double arztKapazitaet = anzahlAerzte * (8.0 * 60.0 / Math.Max(ArztKonfiguration.MITTLERE_BEHANDLUNGSZEIT, 1.0));
        double schwesterKapazitaet = anzahlSchwestern * (8.0 * 60.0 / Math.Max(SchwesterKonfiguration.MITTLERE_SCHWESTER_ZEIT, 1.0));

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
        plot.XAxis.TickLabelStyle(rotation: ergebnis.Achsenwerte.Count > 20 ? 45 : 0);
        plot.YLabel("EUR");
        plot.XLabel(string.Equals(ergebnis.Zeitraum, "Jahr", StringComparison.OrdinalIgnoreCase) ? "Monat" : "Tag");
        plot.Title($"Umsatz und Kosten - Aerzte: {anzahlAerzte}, Schwestern: {anzahlSchwestern} - {ergebnis.Zeitraum}");
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
        plot.XAxis.TickLabelStyle(rotation: ergebnis.Achsenwerte.Count > 20 ? 45 : 0);
        plot.YLabel("Gewinn in EUR");
        plot.XLabel(string.Equals(ergebnis.Zeitraum, "Jahr", StringComparison.OrdinalIgnoreCase) ? "Monat" : "Tag");

        string titel = string.Equals(ergebnis.Zeitraum, "Jahr", StringComparison.OrdinalIgnoreCase)
            ? "Gewinn pro Monat"
            : "Gewinn pro Tag";

        plot.Title($"{titel} - Aerzte: {anzahlAerzte}, Schwestern: {anzahlSchwestern} - {ergebnis.Zeitraum}");
        plot.Legend(location: Alignment.UpperRight);
        plot.Grid(enable: true, lineStyle: LineStyle.Dot);
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
    int Gesamtnachfrage,
    int GesamtBehandelt,
    double Gesamtgewinn,
    double DurchschnittlicherGewinnProEinheit,
    IReadOnlyDictionary<string, double> SaisonGewinn)
{
    // Bequeme Projektionen fuer UI und Berichte, damit dort keine Summenlogik dupliziert wird.
    public IReadOnlyList<string> Achsenwerte => Tagespunkte.Select(p => p.Label).ToList();
    public IReadOnlyList<int> BehandeltePatienten => Tagespunkte.Select(p => p.BehandeltePatienten).ToList();
    public IReadOnlyList<double> GewinnVerlauf => Tagespunkte.Select(p => p.Gewinn).ToList();
    public double GesamtUmsatz => Tagespunkte.Sum(p => p.Umsatz);
    public double Gesamtkosten => Tagespunkte.Sum(p => p.Kosten);
    public double DurchschnittlicherUmsatzProTag => SimulierteTage > 0 ? GesamtUmsatz / SimulierteTage : 0.0;
    public double DurchschnittlicheKostenProTag => SimulierteTage > 0 ? Gesamtkosten / SimulierteTage : 0.0;
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
