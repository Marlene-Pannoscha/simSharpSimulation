using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace simSharpSimulation;

internal sealed partial class FinanzWpfFenster
{
    private TextBox prognoseTextBox = null!;

    private Grid ErstellePrognoseTab()
    {
        Grid inhaltGrid = ErzeugeGeteiltesTabGrid();

        prognoseTextBox = ErzeugeErgebnisTextBox();
        Grid.SetColumn(prognoseTextBox, 0);
        Grid.SetRow(prognoseTextBox, 0);
        inhaltGrid.Children.Add(prognoseTextBox);

        return inhaltGrid;
    }

    private void AktualisierePrognoseTab()
    {
        string prognosePfad = "prognose_report.txt";
        if (File.Exists(prognosePfad))
        {
            prognoseTextBox.Text = File.ReadAllText(prognosePfad);
            return;
        }

        prognoseTextBox.Text = "Kein Prognose-Report gefunden. " +
                               "Starte die SimSharp-Simulation im Konsolenmodus, " +
                               "um den Report zu erzeugen.";
    }
}
