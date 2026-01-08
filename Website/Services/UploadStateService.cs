namespace Website.Services;

/// <summary>
/// Controls the visibility of the UploadModal component used for uploading files.
/// </summary>
public class UploadModalStateService
{
    /// <summary>
    /// Indicates whether the UploadModal component is visible.
    /// </summary>
    public bool IsVisible { get; private set; }

    /// <summary>
    /// Allows to communicate changes in the state of the UploadModal component.
    /// </summary>
    public event Action? OnChanged;

    public long BytesUploaded { get; set; }
    public long TotalSize { get; set; }
    public double TransferSpeedKbs { get; set; }

    /// <summary>
    /// Makes modal visible.
    /// </summary>
    public void Open()
    {
        IsVisible = true;
        BytesUploaded = 0;
        TotalSize = 0;
        // Communicate to everyone who subscribed that state of the service has changed.
        OnChanged?.Invoke();
    }

    public void Close()
    {
        IsVisible = false;
        OnChanged?.Invoke();
    }

    public void NotifyProgress() => OnChanged?.Invoke();
}
