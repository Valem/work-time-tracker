using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace Wtt.App.Web;

/// <summary>
/// The runtime preconnects to a page's host as it starts navigating — a CONNECT through the proxy or a DNS lookup and a
/// socket — below the layer where the virtual host serves the page, so the app's own host name would reach the network.
/// WebView2 has no option for it; the profile's network-prediction preference ("preload pages") governs it, and is set
/// to never before the runtime starts.
/// </summary>
internal static class NetworkPrediction
{
    private const string RuntimeFolderName = "EBWebView";
    private const string ProfileFolderName = "Default";
    private const string PreferencesFileName = "Preferences";
    private const int Never = 2;

    /// <summary>Writes the preference into the profile the runtime will open, keeping the runtime's other preferences.</summary>
    public static void TurnOff(string userDataFolder)
    {
        var path = Path.Combine(ProfileFolder(userDataFolder), PreferencesFileName);
        var preferences = Read(path);
        if (preferences["net"] is not JsonObject net)
        {
            preferences["net"] = net = [];
        }

        if (net["network_prediction_options"] is JsonValue value && value.TryGetValue<int>(out var current) && current == Never)
        {
            return;
        }

        net["network_prediction_options"] = Never;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var written = path + ".wtt";
        File.WriteAllText(written, preferences.ToJsonString());
        File.Move(written, path, overwrite: true);
    }

    /// <exception cref="InvalidOperationException">The runtime opened another profile than the one the preference went to.</exception>
    public static void EnsureTurnedOffFor(CoreWebView2Profile profile, string userDataFolder, WebViewPolicy policy)
    {
        var expected = ProfileFolder(userDataFolder);
        if (policy.EnvironmentOverridesHonoured || WebViewLockdown.SameFolder(profile.ProfilePath, expected))
        {
            return;
        }

        throw new InvalidOperationException($"The web view opened the profile '{profile.ProfilePath}', not '{expected}'.");
    }

    /// <summary>A file the runtime could not read either starts over, as the runtime itself would.</summary>
    private static JsonObject Read(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ProfileFolder(string userDataFolder) => Path.Combine(userDataFolder, RuntimeFolderName, ProfileFolderName);
}
