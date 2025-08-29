using System;

namespace Kantela
{
    /// <summary>
    /// Custom Program class to handle Windows App SDK bootstrap for unpackaged mode
    /// </summary>
    public static class Program
    {
        [System.STAThreadAttribute]
        static void Main(string[] args)
        {
#if DEBUG
            // Initialize Windows App SDK for unpackaged mode in Debug builds
            try
            {
                Microsoft.Windows.ApplicationModel.WindowsAppRuntime.DeploymentManager.Initialize();
            }
            catch (Exception ex)
            {
                // If initialization fails, try without bootstrap
                System.Diagnostics.Debug.WriteLine($"Windows App SDK bootstrap failed: {ex.Message}");
            }
#endif

            // Initialize COM wrappers
            global::WinRT.ComWrappersSupport.InitializeComWrappers();

            // Start the application
            global::Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
    }
}
