namespace Fx3Flasher.App.Services
{
    /// <summary>Abstraction over user dialogs so view models stay testable.</summary>
    public interface IUiInteraction
    {
        bool Confirm(string title, string message);
        string OpenImageFile(string title);
        string SaveTextFile(string suggestedName);
        void ShowError(string message);
    }
}
