﻿// // Copyright (c) Microsoft Corporation.
// // Licensed under the MIT License.

using EventLogExpert.Library.Models;
using EventLogExpert.Store.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.IO.Compression;

namespace EventLogExpert.Shared.Components;

public partial class SettingsModal
{
    private readonly SettingsModel _request = new();

    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    protected override void OnInitialized()
    {
        SubscribeToAction<SettingsAction.OpenMenu>(Load);
        base.OnInitialized();
    }

    private async void Close() => await JSRuntime.InvokeVoidAsync("closeSettingsModal");

    private async void ImportProvider()
    {
        PickOptions options = new()
        {
            PickerTitle = "Please select a database file",
            FileTypes = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".db", ".zip" } }
                })
        };

        try
        {
            var result = await FilePicker.Default.PickMultipleAsync(options);

            Directory.CreateDirectory(Utils.DatabasePath);

            foreach (var item in result)
            {
                var destination = Path.Join(Utils.DatabasePath, item.FileName);
                File.Copy(item.FullPath, destination, true);

                if (Path.GetExtension(destination) == ".zip")
                {
                    ZipFile.ExtractToDirectory(destination, Utils.DatabasePath, overwriteFiles: true);
                    File.Delete(destination);
                }
            }
        }
        catch
        { // TODO: Log Error
        }

        Dispatcher.Dispatch(new SettingsAction.LoadProviders(Utils.DatabasePath));
    }

    private void Load(SettingsAction.OpenMenu action) => _request.TimeZoneId = SettingsState.Value.TimeZoneId;

    private void Save()
    {
        Dispatcher.Dispatch(new SettingsAction.Save(_request, Utils.SettingsPath));
        Close();
    }
}