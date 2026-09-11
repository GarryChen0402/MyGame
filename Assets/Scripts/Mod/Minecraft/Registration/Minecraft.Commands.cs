using System.Reflection;

public partial class Minecraft
{
    // ---- T: commands ----
    private void RegisterCommands()
    {
        // Attribute scan over this assembly's own types (design D3): the
        // scanner registers every [Command] class into the Commands table
        // (registration order = full-name ordinal, so /help listing is stable).
        CommandScanner.ScanAndRegister(Assembly.GetExecutingAssembly());
        // Echo channel bring-up (D6): process-lifetime mirror of the command
        // log, polled by ChatPanelUI; idempotent.
        MirrorSync.Instance.RegisterChatLogSource();
    }
}
