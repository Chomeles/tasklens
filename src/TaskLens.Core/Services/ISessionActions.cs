namespace TaskLens.Core.Services;

/// <summary>Mutating user-session actions — Verbindung trennen / Abmelden (plan-tm3 tm3-08).
/// Failures (access denied, console session) come back as <see cref="ActionResult"/> data.</summary>
public interface ISessionActions
{
    public ActionResult Disconnect(int sessionId);

    public ActionResult Logoff(int sessionId);
}
