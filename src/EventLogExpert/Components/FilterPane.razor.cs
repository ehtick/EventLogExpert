// // Copyright (c) Microsoft Corporation.
// // Licensed under the MIT License.

using EventLogExpert.Eventing.Helpers;
using EventLogExpert.Eventing.Models;
using EventLogExpert.UI;
using EventLogExpert.UI.Models;
using EventLogExpert.UI.Store.EventLog;
using EventLogExpert.UI.Store.FilterCache;
using EventLogExpert.UI.Store.FilterPane;
using EventLogExpert.UI.Store.Settings;
using Fluxor;
using Microsoft.AspNetCore.Components;
using System.Collections.Immutable;
using System.Linq.Dynamic.Core;
using IDispatcher = Fluxor.IDispatcher;

namespace EventLogExpert.Components;

public partial class FilterPane
{
    private readonly FilterDateModel _model = new() { TimeZoneInfo = TimeZoneInfo.Utc };

    private Timer? _advancedFilterDebounceTimer = null;
    private string _advancedFilterErrorMessage = string.Empty;
    private string? _advancedFilterValue = null;
    private bool _canEditAdvancedFilter = true;
    private bool _canEditDate;
    private bool _isAdvancedFilterValid;
    private bool _isFilterListVisible;

    [Inject] private IDispatcher Dispatcher { get; set; } = null!;

    private bool HasFilters =>
        FilterPaneState.Value.CurrentFilters.Any() ||
        FilterPaneState.Value.CachedFilters.Any() ||
        IsDateFilterVisible ||
        IsAdvancedFilterVisible;

    private bool IsAdvancedFilterVisible =>
        _advancedFilterValue is not null || FilterPaneState.Value.AdvancedFilter is not null;

    private bool IsDateFilterVisible => _canEditDate || FilterPaneState.Value.FilteredDateRange is not null;

    private string MenuState => HasFilters ? _isFilterListVisible.ToString().ToLower() : "false";

    [Inject] private IStateSelection<SettingsState, string> TimeZoneState { get; set; } = null!;

    [Inject] private ITraceLogger TraceLogger { get; set; } = null!;

    protected override void OnInitialized()
    {
        SubscribeToAction<FilterPaneAction.ClearAllFilters>(action =>
        {
            _advancedFilterDebounceTimer = null;
            _advancedFilterErrorMessage = string.Empty;
            _advancedFilterValue = null;
            _isAdvancedFilterValid = false;

            _canEditDate = false;
        });

        TimeZoneState.Select(x => x.Config.TimeZoneId);
        TimeZoneState.SelectedValueChanged += (sender, args) => { UpdateFilterDateModel(); };

        FilterPaneState.StateChanged += (sender, args) =>
        {
            if (sender is State<FilterPaneState> filterPaneState)
            {
                Dispatcher.Dispatch(new EventLogAction.SetFilters(GetEventFilter(filterPaneState.Value), TraceLogger));
            }
        };

        base.OnInitialized();
    }

    private static EventLogState.EventFilter GetEventFilter(FilterPaneState filterPaneState)
    {
        return new EventLogState.EventFilter(
            filterPaneState.AdvancedFilter,
            filterPaneState.FilteredDateRange,
            filterPaneState.CachedFilters.Where(f => f.IsEnabled).ToImmutableList(),
            filterPaneState.CurrentFilters.Where(f => f.IsEnabled && f.Comparison.Any())
                .Select(f => f.Comparison.ToImmutableList()).ToImmutableList()
        );
    }

    private static bool TryParseExpression(string? expression, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrEmpty(expression)) { return false; }

        try
        {
            var _ = new List<DisplayEventModel>().AsQueryable()
                .Where(EventLogExpertCustomTypeProvider.ParsingConfig, expression)
                .ToList();

            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private void AddAdvancedFilter()
    {
        _isFilterListVisible = true;
        _canEditAdvancedFilter = true;
        _advancedFilterValue = string.Empty;
    }

    private void AddCachedFilter() => Dispatcher.Dispatch(new FilterCacheAction.OpenMenu());

    private void AddDateFilter()
    {
        _model.TimeZoneInfo = SettingsState.Value.Config.TimeZoneInfo;

        // Round up/down to the nearest hour
        var hourTicks = TimeSpan.FromHours(1).Ticks;

        _model.Before = new DateTime(hourTicks * ((EventLogState.Value.ActiveLogs.Values
            .Where(log => log.Events.Any())
            .Select(log => log.Events.First().TimeCreated)
            .OrderBy(t => t)
            .DefaultIfEmpty(DateTime.UtcNow)
            .Last()
            .Ticks + hourTicks) / hourTicks))
            .ConvertTimeZone(_model.TimeZoneInfo);

        _model.After = new DateTime(hourTicks * (EventLogState.Value.ActiveLogs.Values
            .Where(log => log.Events.Any())
            .Select(log => log.Events.Last().TimeCreated)
            .OrderBy(t => t)
            .DefaultIfEmpty(DateTime.UtcNow)
            .First()
            .Ticks / hourTicks))
            .ConvertTimeZone(_model.TimeZoneInfo);

        _isFilterListVisible = true;
        _canEditDate = true;
    }

    private void AddFilter()
    {
        Dispatcher.Dispatch(new FilterPaneAction.AddFilter());
        _isFilterListVisible = true;
    }

    private void AdvancedFilterChanged(ChangeEventArgs e)
    {
        _advancedFilterValue = e.Value as string;

        _advancedFilterDebounceTimer?.Dispose();

        _advancedFilterDebounceTimer = new(s =>
            {
                _isAdvancedFilterValid = TryParseExpression(s as string, out var message);
                _advancedFilterErrorMessage = message;
                InvokeAsync(StateHasChanged);
            }, e.Value as string, 250, 0);
    }

    private void ApplyAdvancedFilter()
    {
        if (!TryParseExpression(_advancedFilterValue, out _)) { return; }

        _canEditAdvancedFilter = false;
        Dispatcher.Dispatch(new FilterPaneAction.SetAdvancedFilter(_advancedFilterValue));
    }

    private void ApplyDateFilter()
    {
        FilterDateModel model = new()
        {
            After = _model.After.ToUniversalTime(), Before = _model.Before.ToUniversalTime()
        };

        Dispatcher.Dispatch(new FilterPaneAction.SetFilterDateRange(model));
        _canEditDate = false;
    }

    private void EditAdvancedFilter() => _canEditAdvancedFilter = true;

    private void EditDateFilter() => _canEditDate = true;

    private int GetActiveFilters()
    {
        int count = 0;

        count += FilterPaneState.Value.FilteredDateRange?.IsEnabled is true ? 1 : 0;
        count += FilterPaneState.Value.CurrentFilters.Count(filter => filter is { IsEnabled: true, IsEditing: false });
        count += FilterPaneState.Value.CachedFilters.Count(filter => filter is { IsEnabled: true });
        count += FilterPaneState.Value.IsAdvancedFilterEnabled ? 1 : 0;

        return count;
    }

    private void RemoveAdvancedFilter()
    {
        _advancedFilterValue = null;
        Dispatcher.Dispatch(new FilterPaneAction.SetAdvancedFilter(null));
    }

    private void RemoveDateFilter()
    {
        _canEditDate = false;
        Dispatcher.Dispatch(new FilterPaneAction.SetFilterDateRange(null));
    }

    private void ToggleAdvancedFilter() => Dispatcher.Dispatch(new FilterPaneAction.ToggleAdvancedFilter());

    private void ToggleDateFilter() => Dispatcher.Dispatch(new FilterPaneAction.ToggleFilterDate());

    private void ToggleMenu() => _isFilterListVisible = !_isFilterListVisible;

    private void UpdateFilterDateModel()
    {
        var temp = _model.TimeZoneInfo;
        _model.TimeZoneInfo = SettingsState.Value.Config.TimeZoneInfo;

        _model.Before = TimeZoneInfo.ConvertTime(_model.Before, temp, _model.TimeZoneInfo);
        _model.After = TimeZoneInfo.ConvertTime(_model.After, temp, _model.TimeZoneInfo);
    }
}
