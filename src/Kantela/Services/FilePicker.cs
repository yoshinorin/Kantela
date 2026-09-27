using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kantela.Core.Services;
using Kantela.Core.Services.Transfer;
using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;

namespace Kantela.Services;

internal sealed class FilePicker(Func<WindowId> windowIdProvider) : IFilePicker
{
    public async Task<string?> PickOpenFileAsync(BookmarkFormat format)
    {
        FileOpenPicker picker = new(windowIdProvider())
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        foreach (string extension in format.OpenExtensions())
        {
            picker.FileTypeFilter.Add(extension);
        }

        PickFileResult? result = await picker.PickSingleFileAsync();
        return result?.Path;
    }

    public async Task<string?> PickSaveFileAsync(BookmarkFormat format, string suggestedFileName)
    {
        FileSavePicker picker = new(windowIdProvider())
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = format.DefaultExtension(),
        };
        picker.FileTypeChoices.Add(format.DisplayName(), new List<string> { format.DefaultExtension() });

        PickFileResult? result = await picker.PickSaveFileAsync();
        return result?.Path;
    }
}
