using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DeskPets;

static class UpdateChecks
{
    static int checks;
    const string BaseUrl = "https://github.com/Skylar-Duan/desk-pets/releases/";
    static void Require(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }

    static string Release(string tag, string extras)
    {
        return "{\"tag_name\":\"" + tag + "\",\"draft\":false,\"prerelease\":false,\"html_url\":\""
            + BaseUrl + "tag/" + tag + "\",\"assets\":[{\"name\":\"DeskPets.zip\",\"state\":\"uploaded\",\"browser_download_url\":\""
            + BaseUrl + "download/" + tag + "/DeskPets.zip\"}]" + extras + "}";
    }

    static string Asset(string name, string tag, string state)
    {
        return "{\"name\":\"" + name + "\",\"state\":\"" + state
            + "\",\"browser_download_url\":\"" + BaseUrl + "download/" + tag + "/" + name + "\"}";
    }

    static string WithAssets(string assets)
    {
        return "{\"tag_name\":\"v1.2.1\",\"draft\":false,\"prerelease\":false,\"html_url\":\""
            + BaseUrl + "tag/v1.2.1\",\"assets\":[" + assets + "]}";
    }

    static void Reject(string json, string message)
    {
        try { UpdateChecker.ParseReleaseJson(json, "1.2.0"); }
        catch (Exception) { checks++; return; }
        throw new Exception(message);
    }

    static async Task Run()
    {
        Require(UpdateChecker.CompareVersions("1.10.0", "1.2.0") > 0, "Minor versions must compare numerically.");
        Require(UpdateChecker.CompareVersions("v2.0.0", "1.99.99") > 0, "Major versions must compare numerically.");
        Require(UpdateChecker.CompareVersions("v1.2.0+build.77", "1.2.0.0") == 0, "Build metadata must not trigger updates.");
        Require(UpdateChecker.CompareVersions("1.2", "1.2.0.0") == 0, "Legacy tags and assembly versions must agree.");
        Require(UpdateChecker.CompareVersions("1.2.0-rc.2", "1.2.0-rc.10") < 0, "Prerelease identifiers must compare numerically.");
        Require(UpdateChecker.CompareVersions("1.2.0-rc.10", "1.2.0") < 0, "Stable releases outrank previews.");
        Require(UpdateChecker.CompareVersions("9999999999999999999999.0.0", "2.0.0") > 0, "Large future versions must not overflow.");
        UpdateResult newer = UpdateChecker.ParseReleaseJson(Release("v1.10.0", ",\"future_field\":{\"anything\":true}"), "1.2.0");
        Require(newer.Success && newer.HasUpdate && newer.LatestVersion == "1.10.0", "Unknown fields must not break old clients.");
        Require(newer.DownloadUrl == BaseUrl + "download/v1.10.0/DeskPets.zip", "Published zip link must be available.");
        Require(newer.ReleaseUrl == BaseUrl + "tag/v1.10.0", "Details link must be available.");
        string setup = Asset("DeskPets-1.2.1-setup.exe", "v1.2.1", "uploaded");
        string zip = Asset("DeskPets.zip", "v1.2.1", "uploaded");
        string setupUrl = BaseUrl + "download/v1.2.1/DeskPets-1.2.1-setup.exe";
        Require(UpdateChecker.ParseReleaseJson(WithAssets(zip + "," + setup), "1.2.0").DownloadUrl == setupUrl,
            "The installer must win even when ZIP is listed first.");
        Require(UpdateChecker.ParseReleaseJson(WithAssets(setup + "," + zip), "1.2.0").DownloadUrl == setupUrl,
            "The installer must win when listed first.");
        Require(UpdateChecker.ParseReleaseJson(WithAssets(setup), "1.2.0").DownloadUrl == setupUrl,
            "Installer-only releases must offer a download.");
        Require(UpdateChecker.ParseReleaseJson(WithAssets(setup.Replace("uploaded", "new") + "," + zip), "1.2.0").DownloadUrl.EndsWith("DeskPets.zip"),
            "Incomplete installers must fall back to the uploaded ZIP.");
        Require(UpdateChecker.ParseReleaseJson(WithAssets(setup.Replace("DeskPets-1.2.1-setup.exe", "other.exe")), "1.2.0").DownloadUrl == null,
            "Unknown executables must not become installers.");
        Require(UpdateChecker.ParseReleaseJson(WithAssets(setup.Replace("download/v1.2.1/DeskPets-1.2.1-setup.exe", "download/v1.2.1/payload.exe")), "1.2.0").DownloadUrl == null,
            "The asset name and actual downloaded filename must agree.");
        UpdateResult same = UpdateChecker.ParseReleaseJson(Release("v1.2.0", ""), "1.2.0");
        Require(!same.HasUpdate && same.Message.Contains("最新"), "Equal versions must show current status.");
        UpdateResult ahead = UpdateChecker.ParseReleaseJson(Release("v1.1.0", ""), "1.2.0");
        Require(!ahead.HasUpdate && ahead.Message.Contains("高于"), "Local unpublished builds must clearly state they are newer.");
        string noZip = Release("v1.3.0", "").Replace("DeskPets.zip", "unknown.bin");
        Require(UpdateChecker.ParseReleaseJson(noZip, "1.2.0").DownloadUrl == null, "Future asset formats must fall back to release details.");
        Reject(Release("v1.3.0", "").Replace("\"draft\":false", "\"draft\":true"), "Draft was accepted.");
        Reject(Release("v1.3.0", "").Replace("\"prerelease\":false", "\"prerelease\":true"), "Preview was accepted.");
        Reject(Release("v1.3.0-rc.1", ""), "Preview tag was accepted with an incorrect API flag.");
        Reject(Release("latest", ""), "Nonversion tag was accepted.");
        Reject(Release("v01.3.0", ""), "Ambiguous version was accepted.");
        Reject(Release("v1.3.0\\n", ""), "Newline in version was accepted.");
        Reject("[]", "Unexpected response shape was accepted.");
        Reject("{bad json", "Broken JSON was accepted.");
        Reject(new string('x', UpdateChecker.MaximumResponseBytes + 1), "Unbounded response was accepted.");
        Reject(Release("v1.3.0", "").Replace("github.com", "github.com.evil.example"), "Foreign URL was accepted.");
        string[] badUrls = { "http://github.com/Skylar-Duan/desk-pets/releases/tag/v1.3.0",
            "https://github.com@evil.example/Skylar-Duan/desk-pets/releases/tag/v1.3.0",
            BaseUrl + "tag/v1.3.0?token=secret", BaseUrl + "tag/v1.3.0#fragment",
            "https://github.com/Skylar-Duan/another/releases/tag/v1.3.0",
            BaseUrl + "tag/%2f%2fevil.example", BaseUrl + "tag/", BaseUrl + "tag/v1.3.0/extra",
            BaseUrl + "tag/v1.3.0%00", " " + BaseUrl + "tag/v1.3.0" };
        foreach (string bad in badUrls) Require(!UpdateChecker.IsAllowedReleaseUrl(bad, false), "Unsafe details URL accepted: " + bad);
        string badAsset = Release("v1.3.0", "").Replace(BaseUrl + "download/v1.3.0/DeskPets.zip", "https://evil.example/setup.zip");
        Require(UpdateChecker.ParseReleaseJson(badAsset, "1.2.0").DownloadUrl == null, "Foreign download link must be dropped.");

        var offline = new UpdateChecker("1.2.0", token => { throw new WebException("SECRET_URL_TOKEN", WebExceptionStatus.NameResolutionFailure); }, 1000);
        UpdateResult failed = await offline.CheckAsync(CancellationToken.None);
        Require(!failed.Success && failed.Message.Contains("重试") && !failed.Message.Contains("SECRET"), "Offline errors must be retryable and redacted.");
        var broken = new UpdateChecker("1.2.0", token => Task.FromResult("{bad json"), 1000);
        Require(!(await broken.CheckAsync(CancellationToken.None)).Success, "Malformed remote JSON must not escape into UI.");
        var slow = new UpdateChecker("1.2.0", async token => { await Task.Delay(500); return Release("v1.3.0", ""); }, 30);
        var watch = Stopwatch.StartNew();
        UpdateResult timeout = await slow.CheckAsync(CancellationToken.None);
        Require(!timeout.Success && timeout.Message.Contains("超时") && watch.ElapsedMilliseconds < 400, "Deadline must work even if OS/transport ignores cancellation.");
        using (var source = new CancellationTokenSource())
        {
            source.Cancel();
            UpdateResult cancelled = await slow.CheckAsync(source.Token);
            Require(cancelled.Cancelled && !cancelled.Success, "Closed UI cancellation must be recognizable.");
        }
        using (var source = new CancellationTokenSource())
        {
            source.CancelAfter(20);
            var pending = new UpdateChecker("1.2.0", async token => { await Task.Delay(500); return Release("v1.3.0", ""); }, 1000);
            Require((await pending.CheckAsync(source.Token)).Cancelled, "An in-flight check must cancel promptly.");
        }
    }

    static int Main(string[] args)
    {
        try
        {
            Run().GetAwaiter().GetResult();
            Console.WriteLine("PASS: " + checks + " update checks (versions, schema evolution, stable releases, safe links, bounded input, offline, timeout, cancellation).");
            if (Array.IndexOf(args, "--live") >= 0)
            {
                UpdateResult live = new UpdateChecker().CheckAsync(CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine("LIVE: " + live.Message);
                if (!live.Success) return 2;
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
