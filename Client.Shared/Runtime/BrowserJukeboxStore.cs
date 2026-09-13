namespace OpenGarrison.ClientShared;

public static class BrowserJukeboxStore
{
    public static Action? ManageLibrary { get; set; }
    public static Func<Task>? RestoreLibrary { get; set; }
}
