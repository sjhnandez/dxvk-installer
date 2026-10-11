namespace DxvkInstaller.Models;

public sealed class AppSettings
{
    public const string DefaultUpstreamConf = "dxvk.allowFse = True";
    public const string DefaultGplAsyncConf = "dxvk.allowFse = True\r\ndxvk.enableAsync = True";

    public DxvkEdition SelectedEdition { get; set; } = DxvkEdition.GplAsync;
    public string UpstreamConfText { get; set; } = DefaultUpstreamConf;
    public string GplAsyncConfText { get; set; } = DefaultGplAsyncConf;
}
