using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace simSharpSimulation;

internal sealed partial class FinanzWpfFenster
{
    private Grid ErstelleFinanzenTab()
    {
        Grid inhaltGrid = ErzeugeGeteiltesTabGrid();

        // Links stehen Textauswertung und Kennzahlen, rechts die erzeugten Diagramme.
        var ergebnisBox = ErzeugeErgebnisTextBox();
        Grid.SetColumn(ergebnisBox, 0);
        Grid.SetRow(ergebnisBox, 0);
        inhaltGrid.Children.Add(ergebnisBox);
        ergebnisTextBox = ergebnisBox;

        var ticker = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = TextFarbe,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(10, 8, 10, 8)
        };
        var rowDef = new RowDefinition { Height = GridLength.Auto };
        inhaltGrid.RowDefinitions.Add(rowDef);
        Border tickerBorder = new()
        {
            Child = ticker,
            Background = DezenteFlaeche,
            BorderBrush = RandFarbe,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 10, 0, 0)
        };
        Grid.SetRow(tickerBorder, 1);
        inhaltGrid.Children.Add(tickerBorder);
        breakEvenTicker = ticker;

        Grid bilderGrid = new();
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Border finanzenBorder = ErzeugeBildContainer("Umsatz und Kosten", out finanzenImage);
        Grid.SetRow(finanzenBorder, 0);
        bilderGrid.Children.Add(finanzenBorder);

        Border gewinnBorder = ErzeugeBildContainer("Gewinn", out gewinnImage);
        Grid.SetRow(gewinnBorder, 2);
        bilderGrid.Children.Add(gewinnBorder);

        Border kostenstrukturBorder = ErzeugeBildContainer("Gewinn und Kostenstruktur", out kostenstrukturImage);
        Grid.SetRow(kostenstrukturBorder, 4);
        bilderGrid.Children.Add(kostenstrukturBorder);

        ScrollViewer scrollViewer = new ScrollViewer
        {
            Content = bilderGrid,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 4, 0)
        };

        Grid.SetColumn(scrollViewer, 2);
        Grid.SetRow(scrollViewer, 0);
        Grid.SetRowSpan(scrollViewer, 2); // Span over the text and ticker rows
        inhaltGrid.Children.Add(scrollViewer);

        return inhaltGrid;
    }

}
