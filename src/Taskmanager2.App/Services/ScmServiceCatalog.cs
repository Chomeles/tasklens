using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using TaskLens.Core.Models;
using TaskLens.Core.Services;
using Taskmanager2.App.Services.Interop;

namespace Taskmanager2.App.Services;

/// <summary>
/// Read-only <see cref="IServiceCatalog"/> over the service control manager. The enumeration comes
/// from <see cref="ServiceController.GetServices()"/> (name, display name and status are prefilled
/// by EnumServicesStatusEx); PID and description each need one extra per-service query —
/// <c>QueryServiceStatusEx</c> / <c>QueryServiceConfig2</c> — because ServiceController exposes
/// neither. Per-service failures degrade to a null PID / empty description; a failed enumeration
/// degrades the whole snapshot to <see cref="ServiceCatalogAvailability.AccessDenied"/>.
/// No mutation API anywhere (plan-tm2.md §2).
/// </summary>
internal sealed class ScmServiceCatalog : IServiceCatalog
{
    public ServiceCatalogSnapshot Query()
    {
        ServiceController[] controllers;
        try
        {
            controllers = ServiceController.GetServices();
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            return new ServiceCatalogSnapshot([], ServiceCatalogAvailability.AccessDenied);
        }

        var services = new List<ServiceEntry>(controllers.Length);
        foreach (var controller in controllers)
        {
            using (controller)
            {
                var (pid, description) = QueryDetails(controller);
                services.Add(new ServiceEntry(
                    controller.ServiceName,
                    controller.DisplayName,
                    pid,
                    description,
                    Running: controller.Status == ServiceControllerStatus.Running));
            }
        }

        return new ServiceCatalogSnapshot(services, ServiceCatalogAvailability.Available);
    }

    /// <summary>Best effort per service: a denied handle just means no PID and no description.</summary>
    private static (int? Pid, string Description) QueryDetails(ServiceController controller)
    {
        try
        {
            using var handle = controller.ServiceHandle;
            return (QueryPid(handle), QueryDescription(handle));
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            return (null, "");
        }
    }

    private static int? QueryPid(SafeHandle handle)
    {
        if (!Advapi32.QueryServiceStatusEx(
                handle,
                Advapi32.ScStatusProcessInfo,
                out var status,
                Marshal.SizeOf<Advapi32.ServiceStatusProcess>(),
                out _))
        {
            return null;
        }

        // dwProcessId is 0 for stopped services — no process, no PID.
        return status.ProcessId == 0 ? null : (int)status.ProcessId;
    }

    private static string QueryDescription(SafeHandle handle)
    {
        // Two-step query: size probe, then read the SERVICE_DESCRIPTIONW (an LPWSTR at offset 0).
        _ = Advapi32.QueryServiceConfig2(handle, Advapi32.ServiceConfigDescription, IntPtr.Zero, 0, out var needed);
        if (needed <= 0)
        {
            return "";
        }

        var buffer = Marshal.AllocHGlobal(needed);
        try
        {
            if (!Advapi32.QueryServiceConfig2(handle, Advapi32.ServiceConfigDescription, buffer, needed, out _))
            {
                return "";
            }

            return Marshal.PtrToStringUni(Marshal.ReadIntPtr(buffer)) ?? "";
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
