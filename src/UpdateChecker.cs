using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DeskPets
{
    internal sealed class UpdateResult
    {
        public bool Success { get; private set; }
        public bool Cancelled { get; private set; }
        public string CurrentVersion { get; private set; }
        public string LatestVersion { get; private set; }
        public bool HasUpdate { get; private set; }
        public string ReleaseUrl { get; private set; }
        public string DownloadUrl { get; private set; }
        public string Message { get; private set; }

        internal UpdateResult(string current, string latest, bool success, bool cancelled,
            bool hasUpdate, string releaseUrl, string downloadUrl, string message)
        {
            CurrentVersion = current; LatestVersion = latest; Success = success;
            Cancelled = cancelled; HasUpdate = hasUpdate; ReleaseUrl = releaseUrl;
            DownloadUrl = downloadUrl; Message = message;
        }
    }

    // Manual, read-only check. Opening links is the caller's separate user action.
    internal sealed class UpdateChecker
    {
        const string ApiUrl = "https://api.github.com/repos/Skylar-Duan/desk-pets/releases/latest";
        internal const int MaximumResponseBytes = 512 * 1024;
        readonly string currentVersion;
        readonly Func<CancellationToken, Task<string>> fetch;
        readonly int timeoutMilliseconds;
        public string CurrentVersion { get { return currentVersion; } }

        public UpdateChecker() : this(AssemblyVersion(), null, 10000) { }

        // Injectable transport makes offline, cancellation and deadline checks deterministic.
        internal UpdateChecker(string current, Func<CancellationToken, Task<string>> transport, int timeout)
        {
            currentVersion = current;
            fetch = transport ?? FetchReleaseAsync;
            timeoutMilliseconds = timeout;
        }

        static string AssemblyVersion()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            return version.Revision > 0 ? version.ToString(4) : version.ToString(3);
        }

        public async Task<UpdateResult> CheckAsync(CancellationToken cancellationToken)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeoutMilliseconds);
                try
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    Task<string> pending = fetch(deadline.Token);
                    Task finished = await Task.WhenAny(pending, Task.Delay(Timeout.Infinite, deadline.Token)).ConfigureAwait(false);
                    if (finished != pending)
                    {
                        // DNS/OS work may finish later even after Abort. Observe any later failure.
                        ObserveFailure(pending);
                        deadline.Token.ThrowIfCancellationRequested();
                    }
                    string json = await pending.ConfigureAwait(false);
                    deadline.Token.ThrowIfCancellationRequested();
                    return ParseReleaseJson(json, currentVersion);
                }
                catch (Exception error)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return Failure("已取消检查更新。", true);
                    if (deadline.IsCancellationRequested)
                        return Failure("检查更新超时，请检查网络后重试。", false);
                    var webError = error as WebException;
                    if (webError != null)
                    {
                        using (var response = webError.Response as HttpWebResponse)
                        {
                            if (response != null)
                            {
                                if ((int)response.StatusCode == 403 || (int)response.StatusCode == 429)
                                    return Failure("GitHub 暂时限制了检查请求，请稍后重试。", false);
                                if (response.StatusCode == HttpStatusCode.NotFound)
                                    return Failure("暂时没有可用的正式版本，请稍后重试。", false);
                            }
                        }
                        if (webError.Status == WebExceptionStatus.Timeout)
                            return Failure("检查更新超时，请检查网络后重试。", false);
                        return Failure("暂时连不上更新服务，请检查网络后重试。", false);
                    }
                    if (error is IOException)
                        return Failure("连接中断，请检查网络后重试。", false);
                    return Failure("暂时无法读取版本信息，请稍后重试。", false);
                }
            }
        }

        UpdateResult Failure(string message, bool cancelled)
        {
            return new UpdateResult(currentVersion, null, false, cancelled, false, null, null, message);
        }

        static void ObserveFailure(Task pending)
        {
            pending.ContinueWith(task => { var ignored = task.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        static async Task<string> FetchReleaseAsync(CancellationToken token)
        {
            // Older .NET installations otherwise default to TLS 1.0, rejected by GitHub.
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            var request = (HttpWebRequest)WebRequest.Create(ApiUrl);
            request.Method = "GET";
            request.UserAgent = "DeskPets/" + AssemblyVersion();
            request.Accept = "application/vnd.github+json";
            // Pin the protocol, ignore extra JSON properties, and keep old clients usable.
            request.Headers["X-GitHub-Api-Version"] = "2022-11-28";
            request.AllowAutoRedirect = false;
            request.UseDefaultCredentials = false;
            request.Credentials = null;
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            using (token.Register(request.Abort))
            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            {
                if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > MaximumResponseBytes)
                    throw new InvalidDataException("Unexpected update response.");
                using (Stream stream = response.GetResponseStream())
                using (var bytes = new MemoryStream())
                {
                    byte[] buffer = new byte[8192];
                    int length;
                    while ((length = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        if (bytes.Length + length > MaximumResponseBytes)
                            throw new InvalidDataException("Update response is too large.");
                        bytes.Write(buffer, 0, length);
                    }
                    return new UTF8Encoding(false, true).GetString(bytes.ToArray()).TrimStart('\uFEFF');
                }
            }
        }

        internal static UpdateResult ParseReleaseJson(string json, string current)
        {
            if (json == null || json.Length > MaximumResponseBytes)
                throw new FormatException("Invalid release response.");
            var serializer = new JavaScriptSerializer { MaxJsonLength = MaximumResponseBytes, RecursionLimit = 32 };
            var release = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (release == null || !IsFalse(release, "draft") || !IsFalse(release, "prerelease"))
                throw new FormatException("No stable release.");
            string tag = StringField(release, "tag_name");
            SemanticVersion latest = SemanticVersion.Parse(tag);
            if (latest.Prerelease.Length != 0) throw new FormatException("No stable release.");
            string releaseUrl = StringField(release, "html_url");
            if (!IsAllowedReleaseUrl(releaseUrl, false)) throw new FormatException("Invalid release link.");
            string downloadUrl = null;
            string zipUrl = null;
            string installerName = "DeskPets-" + latest.Display + "-setup.exe";
            var assets = release.ContainsKey("assets") ? release["assets"] as IEnumerable : null;
            if (assets != null)
            {
                foreach (object item in assets)
                {
                    var asset = item as Dictionary<string, object>;
                    if (asset == null) continue;
                    string name = StringField(asset, "name");
                    string url = StringField(asset, "browser_download_url");
                    if (StringField(asset, "state") != "uploaded" || !IsAllowedReleaseUrl(url, true)) continue;
                    string assetPath = new Uri(url).AbsolutePath;
                    if (!string.Equals(Uri.UnescapeDataString(assetPath.Substring(assetPath.LastIndexOf('/') + 1)),
                        name, StringComparison.Ordinal)) continue;
                    // Prefer the setup wizard regardless of the API's asset ordering.
                    // Keep ZIP releases usable for existing portable versions.
                    if (name == installerName) downloadUrl = url;
                    else if (name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                        && zipUrl == null) zipUrl = url;
                }
            }
            if (downloadUrl == null) downloadUrl = zipUrl;
            int comparison = latest.CompareTo(SemanticVersion.Parse(current));
            bool hasUpdate = comparison > 0;
            string display = latest.Display;
            return new UpdateResult(current, display, true, false, hasUpdate, releaseUrl, downloadUrl,
                hasUpdate ? "发现新版本 " + display + "，可以查看详情或下载。"
                : comparison < 0 ? "当前版本 " + current + " 高于已发布版本 " + display + "。"
                : "当前已是最新版本（" + current + "）。");
        }

        static bool IsFalse(Dictionary<string, object> value, string key)
        {
            object field;
            return value.TryGetValue(key, out field) && field is bool && !(bool)field;
        }

        static string StringField(Dictionary<string, object> value, string key)
        {
            object field;
            return value.TryGetValue(key, out field) ? field as string : null;
        }

        internal static bool IsAllowedReleaseUrl(string value, bool download)
        {
            Uri uri;
            if (value != null) foreach (char c in value) if (char.IsControl(c)) return false;
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') >= 0
                || value != value.Trim()
                || !Uri.TryCreate(value, UriKind.Absolute, out uri)
                || uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0) return false;
            string prefix = "/Skylar-Duan/desk-pets/releases/" + (download ? "download/" : "tag/");
            if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            string[] segments = uri.AbsolutePath.Substring(prefix.Length).Split('/');
            if (segments.Length != (download ? 2 : 1)) return false;
            foreach (string segment in segments)
            {
                string decoded = Uri.UnescapeDataString(segment);
                foreach (char c in decoded) if (char.IsControl(c)) return false;
                if (decoded.Length == 0 || decoded == "." || decoded == ".."
                    || decoded.IndexOfAny(new char[] { '/', '\\', '\r', '\n' }) >= 0) return false;
            }
            return true;
        }

        internal static int CompareVersions(string left, string right)
        { return SemanticVersion.Parse(left).CompareTo(SemanticVersion.Parse(right)); }

        sealed class SemanticVersion
        {
            static readonly Regex Pattern = new Regex(@"\A[vV]?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:\.(0|[1-9][0-9]*))?(?:\.(0|[1-9][0-9]*))?(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z", RegexOptions.CultureInvariant);
            internal string Display;
            internal string[] Prerelease;
            string[] numbers;

            internal static SemanticVersion Parse(string text)
            {
                if (text == null || text.Length > 128) throw new FormatException("Invalid version.");
                Match match = Pattern.Match(text);
                if (!match.Success) throw new FormatException("Invalid version.");
                var value = new SemanticVersion();
                value.numbers = new string[] { match.Groups[1].Value, match.Groups[2].Value,
                    match.Groups[3].Success ? match.Groups[3].Value : "0", match.Groups[4].Success ? match.Groups[4].Value : "0" };
                value.Prerelease = match.Groups[5].Success ? match.Groups[5].Value.Split('.') : new string[0];
                foreach (string part in value.Prerelease)
                    if (IsNumeric(part) && part.Length > 1 && part[0] == '0') throw new FormatException("Invalid prerelease.");
                value.Display = text.TrimStart('v', 'V');
                return value;
            }

            internal int CompareTo(SemanticVersion other)
            {
                for (int i = 0; i < numbers.Length; i++)
                { int result = CompareNumber(numbers[i], other.numbers[i]); if (result != 0) return result; }
                if (Prerelease.Length == 0 || other.Prerelease.Length == 0)
                    return Prerelease.Length == other.Prerelease.Length ? 0 : (Prerelease.Length == 0 ? 1 : -1);
                for (int i = 0; i < Math.Min(Prerelease.Length, other.Prerelease.Length); i++)
                {
                    string left = Prerelease[i], right = other.Prerelease[i];
                    bool leftNumeric = IsNumeric(left), rightNumeric = IsNumeric(right);
                    int result = leftNumeric && rightNumeric ? CompareNumber(left, right)
                        : leftNumeric != rightNumeric ? (leftNumeric ? -1 : 1) : string.CompareOrdinal(left, right);
                    if (result != 0) return result;
                }
                return Prerelease.Length.CompareTo(other.Prerelease.Length);
            }

            static bool IsNumeric(string text)
            { foreach (char c in text) if (c < '0' || c > '9') return false; return true; }

            static int CompareNumber(string left, string right)
            { return left.Length != right.Length ? left.Length.CompareTo(right.Length) : string.CompareOrdinal(left, right); }
        }
    }
}
