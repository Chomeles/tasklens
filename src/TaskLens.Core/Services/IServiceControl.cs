namespace TaskLens.Core.Services;

/// <summary>Mutating service actions — Starten / Beenden / Neu starten (plan-tm3 tm3-07).
/// Failures (access denied, timeout, service refuses) come back as <see cref="ActionResult"/> data.</summary>
public interface IServiceControl
{
    public ActionResult Start(string serviceName);

    public ActionResult Stop(string serviceName);

    public ActionResult Restart(string serviceName);
}
