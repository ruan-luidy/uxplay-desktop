using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using UxPlayDesktop.ViewModels;

namespace UxPlayDesktop.Views;

public partial class LogsView : UserControl
{
    private LogsViewModel? _logs;

    public LogsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_logs != null)
        {
            _logs.Logs.CollectionChanged -= OnLogsChanged;
        }

        _logs = (DataContext as MainViewModel)?.Logs;
        if (_logs != null)
        {
            _logs.Logs.CollectionChanged += OnLogsChanged;
        }
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _logs?.AutoScroll != true)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (LogsListView.Items.Count > 0)
            {
                LogsListView.ScrollIntoView(LogsListView.Items[^1]);
            }
        });
    }
}
