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

        StackPanel diagrammPanel = new()
        {
            Orientation = Orientation.Vertical
        };

        Border phaseBorder = ErzeugeBildContainer("Prognose-Trefferquote je Phase", out prognosePhaseImage);
        phaseBorder.MinHeight = 420;
        phaseBorder.Margin = new Thickness(0, 0, 0, 12);
        diagrammPanel.Children.Add(phaseBorder);

        Border scatterBorder = ErzeugeBildContainer("Prognose Restzeit vs. Ist-Restzeit", out prognoseScatterImage);
        scatterBorder.MinHeight = 420;
        scatterBorder.Margin = new Thickness(0, 0, 0, 12);
        diagrammPanel.Children.Add(scatterBorder);

        Border abbruecheZeitBorder = ErzeugeBildContainer("Prognose-Abbrüche über Zeit", out prognoseAbbruecheZeitImage);
        abbruecheZeitBorder.MinHeight = 420;
        abbruecheZeitBorder.Margin = new Thickness(0, 0, 0, 12);
        diagrammPanel.Children.Add(abbruecheZeitBorder);

        Border abbruchgruendeBorder = ErzeugeBildContainer("Abbruchgründe Vergleich", out prognoseAbbruchgruendeImage);
        abbruchgruendeBorder.MinHeight = 420;
        diagrammPanel.Children.Add(abbruchgruendeBorder);

        ScrollViewer diagrammScrollViewer = new()
        {
            Content = diagrammPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        Grid.SetColumn(diagrammScrollViewer, 2);
        Grid.SetRow(diagrammScrollViewer, 0);
        inhaltGrid.Children.Add(diagrammScrollViewer);

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
