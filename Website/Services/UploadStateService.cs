namespace Website.Services;

/// <summary>
/// Controls the visibility of the UploadModal component used for uploading files.
/// </summary>
public class UploadModalStateService
{
    public bool IsVisible { get; private set; }

    /// <summary>
    /// Allows to communicate changes in the state of the UploadModal component.
    /// </summary>
    public event Action? OnChanged;

    /// <summary>
    /// Makes modal visible.
    /// </summary>
    public void Open()
    {
        IsVisible = true;
        // Communicate to everyone who subscribed that state of the service has changed.
        OnChanged?.Invoke();
    }

    public void Close()
    {
        IsVisible = false;
        OnChanged?.Invoke();
    }
}
