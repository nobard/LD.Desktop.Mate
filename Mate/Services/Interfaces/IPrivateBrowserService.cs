namespace Mate.Services.Interfaces;

public interface IPrivateBrowserService
{
    PrivateBrowserOpenResult OpenSearch(string query);
}

public enum PrivateBrowserOpenResult
{
    Opened,
    UnsupportedBrowser,
    BrowserNotFound
}
