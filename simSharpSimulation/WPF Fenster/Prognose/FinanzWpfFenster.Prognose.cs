using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace simSharpSimulation;

internal sealed partial class FinanzWpfFenster
{
    private TextBox prognoseTextBox = null!;
    private Image prognosePhaseImage = null!;
    private Image prognoseScatterImage = null!;
    private Image prognoseAbbruecheZeitImage = null!;
    private Image prognoseAbbruchgruendeImage = null!;

    private Grid ErstellePrognoseTab()
    {
        Grid inhaltGrid = ErzeugeGeteiltesTabGrid();

        prognoseTextBox = ErzeugeErgebnisTextBox();
        Grid.SetColumn(prognoseTextBox, 0);
        Grid.SetRow(prognoseTextBox, 0);
        inhaltGrid.Children.Add(prognoseTextBox);

        Grid diagrammGrid = new();
        diagrammGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        diagrammGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        diagrammGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        diagrammGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        diagrammGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        diagrammGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Border phaseBorder = ErzeugeBildContainer("Prognose-Trefferquote je Phase", out prognosePhaseImage);
        Grid.SetColumn(phaseBorder, 0);
        Grid.SetRow(phaseBorder, 0);
        diagrammGrid.Children.Add(phaseBorder);

        Border scatterBorder = ErzeugeBildContainer("Prognose Restzeit vs. Ist-Restzeit", out prognoseScatterImage);
        Grid.SetColumn(scatterBorder, 2);
        Grid.SetRow(scatterBorder, 0);
        diagrammGrid.Children.Add(scatterBorder);

        Border abbruecheZeitBorder = ErzeugeBildContainer("Prognose-Abbrüche über Zeit", out prognoseAbbruecheZeitImage);
        Grid.SetColumn(abbruecheZeitBorder, 0);
        Grid.SetRow(abbruecheZeitBorder, 2);
        diagrammGrid.Children.Add(abbruecheZeitBorder);

        Border abbruchgruendeBorder = ErzeugeBildContainer("Abbruchgründe Vergleich", out prognoseAbbruchgruendeImage);
        Grid.SetColumn(abbruchgruendeBorder, 2);
        Grid.SetRow(abbruchgruendeBorder, 2);
        diagrammGrid.Children.Add(abbruchgruendeBorder);

        Grid.SetColumn(diagrammGrid, 2);
        Grid.SetRow(diagrammGrid, 0);
        inhaltGrid.Children.Add(diagrammGrid);

        return inhaltGrid;
    }

    private void AktualisierePrognoseTab()
    {
        string prognosePfad = "prognose_report.txt";
        string prognoseJsonPfad = "prognose_daten.json";
        if (File.Exists(prognosePfad))
        {
            prognoseTextBox.Text = File.ReadAllText(prognosePfad);

            if (File.Exists(prognoseJsonPfad))
            {
                PrognoseVisualisierung.PrognoseDiagrammPfade pfade =
                    PrognoseVisualisierung.ErzeugeDiagramme(prognoseJsonPfad);

                prognosePhaseImage.Source = LadeBild(pfade.TrefferquoteJePhasePfad);
                prognoseScatterImage.Source = LadeBild(pfade.RestzeitScatterPfad);
                prognoseAbbruecheZeitImage.Source = LadeBild(pfade.PrognoseAbbruecheZeitPfad);
                prognoseAbbruchgruendeImage.Source = LadeBild(pfade.AbbruchgruendePfad);
            }

            return;
        }

        prognoseTextBox.Text = "Kein Prognose-Report gefunden. " +
                               "Starte die SimSharp-Simulation im Konsolenmodus, " +
                               "um den Report zu erzeugen.";
        prognosePhaseImage.Source = null;
        prognoseScatterImage.Source = null;
        prognoseAbbruecheZeitImage.Source = null;
        prognoseAbbruchgruendeImage.Source = null;
    }
}
