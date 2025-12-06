namespace MiniERP.UI.Interface
{
    public interface IDialogService
    {
        void ShowError(string message, string title = "Error");
        void ShowWarning(string message, string title = "Warning");
        bool ShowConfirm(string message, string title = "Confirm");
    }
}
