using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.App.Services;

namespace Taskmanager2.App.Services;

/// <summary>
/// „Netzwerkverbindungen" dialog (tm2r-03) — the TCPView replacement: the right-clicked process's
/// open TCP/UDP endpoints from <see cref="NetConnectionEnumerator"/>, refreshable in place. Built
/// in code like <see cref="RunTaskDialog"/> so the Prozesse and Details pages share one
/// implementation. App rows pass every PID of their process group; Details passes the exact PID.
/// </summary>
internal static class NetworkConnectionsDialog
{
    internal static async System.Threading.Tasks.Task ShowAsync(
        XamlRoot xamlRoot, string processName, IReadOnlyCollection<int> pids)
    {
        var table = new Grid { ColumnSpacing = 16, RowSpacing = 4, MinWidth = 480 };
        for (var i = 0; i < 4; i++)
        {
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        Fill(table, pids);

        var dialog = new ContentDialog
        {
            Title = $"Netzwerkverbindungen – {processName}",
            PrimaryButtonText = "Aktualisieren",
            CloseButtonText = "Schließen",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
            Content = new ScrollViewer
            {
                Content = table,
                MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Enabled,
            },
        };

        // „Aktualisieren" re-queries in place; Cancel keeps the dialog open.
        dialog.PrimaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            Fill(table, pids);
        };

        await dialog.ShowAsync();
    }

    private static void Fill(Grid table, IReadOnlyCollection<int> pids)
    {
        table.RowDefinitions.Clear();
        table.Children.Clear();

        AddRow(table, 0, header: true, "Protokoll", "Lokale Adresse", "Remoteadresse", "Status");

        var rows = NetConnectionEnumerator.Query(pids);
        if (rows.Count == 0)
        {
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var empty = new TextBlock
            {
                Text = "Keine offenen Verbindungen.",
                Opacity = 0.6,
                Margin = new Thickness(0, 8, 0, 0),
            };
            Grid.SetRow(empty, 1);
            Grid.SetColumnSpan(empty, 4);
            table.Children.Add(empty);
            return;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            AddRow(table, i + 1, header: false, row.Protocol, row.LocalAddress, row.RemoteAddress, row.State);
        }
    }

    private static void AddRow(Grid table, int rowIndex, bool header, params string[] cells)
    {
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var column = 0; column < cells.Length; column++)
        {
            var cell = new TextBlock
            {
                Text = cells[column],
                FontWeight = header ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            };
            Grid.SetRow(cell, rowIndex);
            Grid.SetColumn(cell, column);
            table.Children.Add(cell);
        }
    }
}
