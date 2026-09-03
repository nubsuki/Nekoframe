using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace Nekoframe;

public static class UpdateChecker
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/nubsuki/Nekoframe/releases/latest";
    private static readonly HttpClient HttpClient = new();

    static UpdateChecker()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Nekoframe", "1.0"));
        HttpClient.Timeout = TimeSpan.FromSeconds(6);
    }

    public record UpdateResult(bool HasUpdate, Version? LatestVersion, string ReleaseUrl);

    public static async Task<UpdateResult> CheckAsync(Version? currentVersion)
    {
        if (currentVersion == null)
            return new UpdateResult(false, null, "");

        try
        {
            var jsonStr = await HttpClient.GetStringAsync(LatestReleaseApiUrl);
            var json = JObject.Parse(jsonStr);

            var tagName = json["tag_name"]?.ToString() ?? "";
            var htmlUrl = json["html_url"]?.ToString() ?? "https://github.com/nubsuki/Nekoframe/releases";

            var cleanTag = tagName.TrimStart('v', 'V').Trim();
            if (TryParseVersion(cleanTag, out var latestVer))
            {
                var curNorm = new Version(Math.Max(0, currentVersion.Major), Math.Max(0, currentVersion.Minor), Math.Max(0, currentVersion.Build));
                var latNorm = new Version(Math.Max(0, latestVer.Major), Math.Max(0, latestVer.Minor), Math.Max(0, latestVer.Build));

                if (latNorm > curNorm)
                {
                    return new UpdateResult(true, latestVer, htmlUrl);
                }
            }
        }
        catch
        {
            // Fail silently if offline, rate limited, or connection interrupted
        }

        return new UpdateResult(false, null, "");
    }

    private static bool TryParseVersion(string input, out Version version)
    {
        var parts = input.Split('.');
        if (parts.Length == 2)
            input += ".0";

        return Version.TryParse(input, out version!);
    }

    public static void OpenReleaseUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }
}
