using System.ComponentModel;
using System.ServiceProcess;
using TaskLens.Core.Services;

namespace Taskmanager2.App.Services;

/// <summary>
/// Dienste-Steuerung on <see cref="ServiceController"/> (tm3-07). Every failure mode — access
/// denied, dependent services, timeout while the SCM transitions — returns as
/// <see cref="ActionResult"/> data, never as an exception reaching a view.
/// </summary>
internal sealed class ScmServiceControl : IServiceControl
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public ActionResult Start(string serviceName) => Run(serviceName, sc =>
    {
        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, Timeout);
    });

    public ActionResult Stop(string serviceName) => Run(serviceName, sc =>
    {
        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped, Timeout);
    });

    public ActionResult Restart(string serviceName) => Run(serviceName, sc =>
    {
        if (sc.Status != ServiceControllerStatus.Stopped)
        {
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, Timeout);
        }

        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, Timeout);
    });

    private static ActionResult Run(string serviceName, Action<ServiceController> action)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            action(sc);
            return ActionResult.Ok;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or System.ServiceProcess.TimeoutException or ArgumentException)
        {
            return ActionResult.Fail(ex.InnerException?.Message ?? ex.Message);
        }
    }
}
