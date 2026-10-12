namespace simSharpSimulation;

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
    public double GesamtEnergiekosten => Tagesergebnisse.Sum(t => t.Kosten.Energiekosten);
    public double GesamtReinigungskosten => Tagesergebnisse.Sum(t => t.Kosten.Reinigungskosten);
    public double GesamtInfrastrukturkosten => Tagesergebnisse.Sum(t => t.Kosten.Infrastrukturkosten);
    public double GesamtITUndVerwaltungskosten => Tagesergebnisse.Sum(t => t.Kosten.ITUndVerwaltungskosten);
    public double GesamtVersicherungskosten => Tagesergebnisse.Sum(t => t.Kosten.Versicherungskosten);
    public double GesamtMaterialkosten => Tagesergebnisse.Sum(t => t.Kosten.MedizinischesMaterialkosten);
    public double GesamtLeasingkosten => Tagesergebnisse.Sum(t => t.Kosten.GeraeteLeasingKosten);
    public double GesamtWartungskosten => Tagesergebnisse.Sum(t => t.Kosten.GeraeteWartungskosten);
    public double GesamtSonstigeFixkosten => Tagesergebnisse.Sum(t => t.Kosten.SonstigeFixkosten);
    public double GesamtITVerwaltungVersicherung => GesamtITUndVerwaltungskosten + GesamtVersicherungskosten;
    public double GesamtRaumkosten => GesamtMietkosten + GesamtEnergiekosten + GesamtReinigungskosten;
    public double GesamtGeraetekosten => GesamtLeasingkosten + GesamtWartungskosten;
    public double DurchschnittlicherUmsatzProTag => SimulierteTage > 0 ? GesamtUmsatz / SimulierteTage : 0.0;
    public double DurchschnittlicheKostenProTag => SimulierteTage > 0 ? Gesamtkosten / SimulierteTage : 0.0;
    public Tageskosten DurchschnittlicheTageskosten => SimulierteTage > 0
        ? new Tageskosten(
            Tagesergebnisse.Sum(t => t.Kosten.Arztlohn) / SimulierteTage,
            Tagesergebnisse.Sum(t => t.Kosten.Schwesterlohn) / SimulierteTage,
            Tagesergebnisse.Sum(t => t.Kosten.Rezeptionlohn) / SimulierteTage,
            GesamtMietkosten / SimulierteTage,
            GesamtEnergiekosten / SimulierteTage,
            GesamtReinigungskosten / SimulierteTage,
            GesamtInfrastrukturkosten / SimulierteTage,
            GesamtITUndVerwaltungskosten / SimulierteTage,
            GesamtVersicherungskosten / SimulierteTage,
            GesamtMaterialkosten / SimulierteTage,
            GesamtLeasingkosten / SimulierteTage,
            GesamtWartungskosten / SimulierteTage,
            GesamtSonstigeFixkosten / SimulierteTage)
        : new Tageskosten(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
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
            Tagespunkte.Sum(p => p.Behandlungsmix.LangPatienten));
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
