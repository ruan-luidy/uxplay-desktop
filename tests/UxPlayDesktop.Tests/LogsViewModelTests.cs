using System.Linq;
using UxPlayDesktop.Models;
using UxPlayDesktop.ViewModels;
using Xunit;

namespace UxPlayDesktop.Tests;

public class LogsViewModelTests
{
    [Fact]
    public void LogsViewModel_AddEntry_UpdatesHasLogsAndList()
    {
        var vm = new LogsViewModel();
        Assert.False(vm.HasLogs);

        vm.AddEntry(new LogEntry { Level = LogLevel.Info, Message = "Engine initialized" });

        Assert.True(vm.HasLogs);
        Assert.Single(vm.Logs);
        Assert.Equal("Engine initialized", vm.Logs[0].Message);
    }

    [Fact]
    public void LogsViewModel_ClearCommand_RemovesAllLogs()
    {
        var vm = new LogsViewModel();
        vm.AddEntry(new LogEntry { Level = LogLevel.Info, Message = "Message 1" });
        vm.AddEntry(new LogEntry { Level = LogLevel.Warning, Message = "Message 2" });
        Assert.Equal(2, vm.Logs.Count);

        vm.ClearCommand.Execute(null);

        Assert.Empty(vm.Logs);
        Assert.False(vm.HasLogs);
    }

    [Fact]
    public void LogsViewModel_FilterText_SyncsWithSearchFilter()
    {
        var vm = new LogsViewModel();
        vm.FilterText = "keyword";

        Assert.Equal("keyword", vm.SearchFilter);
        Assert.Equal("keyword", vm.FilterText);
    }

    [Fact]
    public void LogsViewModel_FilteredLogs_ReturnsCollection()
    {
        var vm = new LogsViewModel();
        vm.AddEntry(new LogEntry { Level = LogLevel.Info, Message = "AirPlay stream active" });

        Assert.NotNull(vm.FilteredLogs);
        var list = vm.FilteredLogs.Cast<LogEntry>().ToList();
        Assert.Single(list);
        Assert.Equal("AirPlay stream active", list[0].Message);
    }

    [Fact]
    public void LogsViewModel_CountsWarningsAndErrors()
    {
        var vm = new LogsViewModel();
        vm.AddEntry(new LogEntry { Level = LogLevel.Info, Message = "Started" });
        vm.AddEntry(new LogEntry { Level = LogLevel.Warning, Message = "Jitter" });
        vm.AddEntry(new LogEntry { Level = LogLevel.Error, Message = "Sink missing" });
        vm.AddEntry(new LogEntry { Level = LogLevel.Warning, Message = "Underrun" });

        Assert.Equal(2, vm.WarningCount);
        Assert.Equal(1, vm.ErrorCount);

        vm.ClearLogs();

        Assert.Equal(0, vm.WarningCount);
        Assert.Equal(0, vm.ErrorCount);
    }

    [Fact]
    public void LogsViewModel_LevelFilter_ShowsOnlyThatLevel()
    {
        var vm = new LogsViewModel();
        vm.AddEntry(new LogEntry { Level = LogLevel.Info, Message = "Started" });
        vm.AddEntry(new LogEntry { Level = LogLevel.Error, Message = "Sink missing" });

        vm.SelectedLevelFilter = LogLevel.Error;

        Assert.False(vm.IsAllLevels);
        Assert.Equal("Sink missing", Assert.Single(vm.FilteredLogs.Cast<LogEntry>()).Message);

        vm.IsAllLevels = true;

        Assert.Null(vm.SelectedLevelFilter);
        Assert.Equal(2, vm.FilteredLogs.Cast<LogEntry>().Count());
    }
}
