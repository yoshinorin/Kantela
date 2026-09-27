using System;
using System.Threading.Tasks;
using Kantela.Core.Services;
using Windows.System;

namespace Kantela.Services;

internal sealed class BrowserLauncher : IBrowserLauncher
{
    public async Task<bool> OpenAsync(Uri uri) => await Launcher.LaunchUriAsync(uri);
}
