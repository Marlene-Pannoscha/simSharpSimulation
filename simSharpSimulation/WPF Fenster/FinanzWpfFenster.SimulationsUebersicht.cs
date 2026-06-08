using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace simSharpSimulation;

internal sealed partial class FinanzWpfFenster
{
    private Grid ErstelleSimulationsUebersichtTab()
    {
        Grid inhaltGrid = new();
        inhaltGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        simulationsUebersichtTextBox = ErzeugeErgebnisTextBox();
        Grid.SetRow(simulationsUebersichtTextBox, 0);
        inhaltGrid.Children.Add(simulationsUebersichtTextBox);

        return inhaltGrid;
    }

    private void AktualisiereSimulationsUebersicht(SimulationsDaten? daten)
    {
        if (daten is null)
        {
            simulationsUebersichtTextBox.Text = "Keine Simulationsuebersicht vorhanden.\n\nStarte die Simulation, um die Kennzahlen aus der Konsolenausgabe hier anzuzeigen.";
            return;
        }

        int behandeltePatientenGesamt = daten.Gesamtprozesszeiten.Count;
        int behandeltePatientenProTag = (int)Math.Round(behandeltePatientenGesamt / (double)Program.SimulierteArbeitstage);
        Tagesergebnis finanzenProTag = FinanzRechner.BerechneTagesergebnis(ArztKonfiguration.ANZAHL_AERZTE, behandeltePatientenProTag);

        simulationsUebersichtTextBox.Text = ErzeugeSimulationsUebersichtText(daten, behandeltePatientenProTag, finanzenProTag);
    }

    private static string ErzeugeSimulationsUebersichtText(
        SimulationsDaten daten,
        int behandeltePatientenProTag,
        Tagesergebnis finanzenProTag)
    {
        int anzahlMitTermin = daten.GesamtprozesszeitenMitTermin.Count;
        int anzahlOhneTermin = daten.GesamtprozesszeitenOhneTermin.Count;
        int gesamtTypen = daten.PatientenTypZaehler.Values.Sum();
        int nichtBehandeltGesamt = daten.AnzahlNichtBehandeltRezeptionGesamt
            + daten.AnzahlNichtBehandeltSchwesterGesamt
            + daten.AnzahlNichtBehandeltArztGesamt;

        double gesamtSumMitTermin = daten.DurchschnittlicheWartezeitRezeptionMitTermin
            + daten.DurchschnittlicheBehandlungszeitRezeptionMitTermin
            + daten.DurchschnittlicheWartezeitSchwesterMitTermin
            + daten.DurchschnittlicheBehandlungszeitSchwesterMitTermin
            + daten.DurchschnittlicheWartezeitArztMitTermin
            + daten.DurchschnittlicheBehandlungszeitArztMitTermin;

        double gesamtSumOhneTermin = daten.DurchschnittlicheWartezeitRezeptionOhneTermin
            + daten.DurchschnittlicheBehandlungszeitRezeptionOhneTermin
            + daten.DurchschnittlicheWartezeitSchwesterOhneTermin
            + daten.DurchschnittlicheBehandlungszeitSchwesterOhneTermin
            + daten.DurchschnittlicheWartezeitArztOhneTermin
            + daten.DurchschnittlicheBehandlungszeitArztOhneTermin;

        StringBuilder sb = new();
        sb.AppendLine("Simulationsuebersicht");
        sb.AppendLine(new string('=', 72));
        sb.AppendLine($"Simulation beendet. {daten.EchteAnkunftszeiten.Count.ToString("N0", DeCulture)} Patienten empfangen.");
        sb.AppendLine($"Simulierte Arbeitstage: {Program.SimulierteArbeitstage.ToString("N0", DeCulture)}");
        sb.AppendLine($"Behandelte Patienten gesamt: {daten.Gesamtprozesszeiten.Count.ToString("N0", DeCulture)}");
        sb.AppendLine($"Behandelte Patienten pro Tag: {behandeltePatientenProTag.ToString("N0", DeCulture)}");
        sb.AppendLine($"Anzahl Aerzte: {ArztKonfiguration.ANZAHL_AERZTE.ToString("N0", DeCulture)}");
        sb.AppendLine($"Anzahl Schwestern: {SchwesterKonfiguration.ANZAHL_SCHWESTERN.ToString("N0", DeCulture)}");
        sb.AppendLine($"Anzahl Rezeption: {RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN.ToString("N0", DeCulture)}");
        sb.AppendLine();
        sb.AppendLine("Durchschnittliche Zeiten");
        sb.AppendLine($"Wartezeit Rezeption: {daten.DurchschnittlicheWartezeitRezeption.ToString("N2", DeCulture)} Minuten");
        sb.AppendLine($"Wartezeit Schwester: {daten.DurchschnittlicheWartezeitSchwester.ToString("N2", DeCulture)} Minuten");
        sb.AppendLine($"Wartezeit Arzt: {daten.DurchschnittlicheWartezeitArzt.ToString("N2", DeCulture)} Minuten");
        sb.AppendLine($"Gesamtprozesszeit: {daten.DurchschnittlicheGesamtprozesszeit.ToString("N2", DeCulture)} Minuten");
        sb.AppendLine();
        sb.AppendLine("Nicht behandelte Patienten");
        sb.AppendLine($"Gesamt: {nichtBehandeltGesamt.ToString("N0", DeCulture)}");
        sb.AppendLine($"Davon Rezeption-Schichtende: {daten.AnzahlNichtBehandeltRezeptionFeierabend.ToString("N0", DeCulture)}");
        sb.AppendLine($"Davon Schwester-Schichtende: {daten.AnzahlNichtBehandeltSchwesterFeierabend.ToString("N0", DeCulture)}");
        sb.AppendLine($"Davon Arzt-Schichtende: {daten.AnzahlNichtBehandeltArztFeierabend.ToString("N0", DeCulture)}");
        sb.AppendLine();
        sb.AppendLine("Vergleich mit Termin vs. ohne Termin");
        sb.AppendLine($"{"Gruppe",-12} | {"Anz",6} | {"Rezept.W",9} | {"Rezept.B",9} | {"Schwest.W",10} | {"Schwest.B",10} | {"Arzt.W",9} | {"Arzt.B",9} | {"Gesamt",9}");
        sb.AppendLine(new string('-', 112));
        sb.AppendLine(ErzeugeTerminVergleichZeile(
            "Mit Termin",
            anzahlMitTermin,
            daten.DurchschnittlicheWartezeitRezeptionMitTermin,
            daten.DurchschnittlicheBehandlungszeitRezeptionMitTermin,
            daten.DurchschnittlicheWartezeitSchwesterMitTermin,
            daten.DurchschnittlicheBehandlungszeitSchwesterMitTermin,
            daten.DurchschnittlicheWartezeitArztMitTermin,
            daten.DurchschnittlicheBehandlungszeitArztMitTermin,
            gesamtSumMitTermin));
        sb.AppendLine(ErzeugeTerminVergleichZeile(
            "Ohne Termin",
            anzahlOhneTermin,
            daten.DurchschnittlicheWartezeitRezeptionOhneTermin,
            daten.DurchschnittlicheBehandlungszeitRezeptionOhneTermin,
            daten.DurchschnittlicheWartezeitSchwesterOhneTermin,
            daten.DurchschnittlicheBehandlungszeitSchwesterOhneTermin,
            daten.DurchschnittlicheWartezeitArztOhneTermin,
            daten.DurchschnittlicheBehandlungszeitArztOhneTermin,
            gesamtSumOhneTermin));
        sb.AppendLine();
        sb.AppendLine("Patienten-Typen: Verteilung und Wartezeiten");
        sb.AppendLine($"{"Typ",-10} | {"Anzahl",8} | {"Anteil (%)",10} | {"Arzt (min)",12} | {"Schwester (min)",17}");
        sb.AppendLine(new string('-', 69));
        foreach ((PatientenTyp typ, int anzahl) in daten.PatientenTypZaehler)
        {
            double anteil = gesamtTypen > 0 ? anzahl * 100.0 / gesamtTypen : 0.0;
            sb.AppendLine($"{typ,-10} | {anzahl,8:N0} | {anteil,10:N2} | {daten.DurchschnittlicheArztWartezeitNachTyp(typ),12:N2} | {daten.DurchschnittlicheSchwesterWartezeitNachTyp(typ),17:N2}");
        }
        sb.AppendLine();
        sb.AppendLine("Finanzen Tagesuebersicht");
        sb.AppendLine($"Umsatz: {FinanzVisualisierung.FormatEuro(finanzenProTag.Umsatz)}");
        sb.AppendLine($"Arztlohn: {FinanzVisualisierung.FormatEuro(finanzenProTag.Kosten.Arztlohn)}");
        sb.AppendLine($"Schwesterlohn: {FinanzVisualisierung.FormatEuro(finanzenProTag.Kosten.Schwesterlohn)}");
        sb.AppendLine($"Rezeptionlohn: {FinanzVisualisierung.FormatEuro(finanzenProTag.Kosten.Rezeptionlohn)}");
        sb.AppendLine($"Fixkosten: {FinanzVisualisierung.FormatEuro(finanzenProTag.Kosten.Fixkosten)}");
        sb.AppendLine($"Behandlungskosten: {FinanzVisualisierung.FormatEuro(finanzenProTag.Kosten.Behandlungskosten)}");
        sb.AppendLine($"Gesamtkosten: {FinanzVisualisierung.FormatEuro(finanzenProTag.Kosten.Gesamtkosten)}");
        sb.AppendLine($"Gewinn: {FinanzVisualisierung.FormatEuro(finanzenProTag.Gewinn)}");

        return sb.ToString();
    }

    private static string ErzeugeTerminVergleichZeile(
        string gruppe,
        int anzahl,
        double rezeptionsWartezeit,
        double rezeptionsBehandlungszeit,
        double schwesterWartezeit,
        double schwesterBehandlungszeit,
        double arztWartezeit,
        double arztBehandlungszeit,
        double gesamt)
    {
        return $"{gruppe,-12} | {anzahl,6:N0} | {rezeptionsWartezeit,9:N2} | {rezeptionsBehandlungszeit,9:N2} | {schwesterWartezeit,10:N2} | {schwesterBehandlungszeit,10:N2} | {arztWartezeit,9:N2} | {arztBehandlungszeit,9:N2} | {gesamt,9:N2}";
    }
}
