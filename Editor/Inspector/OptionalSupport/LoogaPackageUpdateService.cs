using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace LoogaSoft.Inspector.Editor
{
    internal enum LoogaPackageUpdateStatus
    {
        Checking,
        Current,
        UpdateAvailable,
        UnreleasedChanges,
        LocalDevelopment,
        Unavailable
    }

    internal sealed class LoogaPackageUpdateInfo
    {
        public string PackageName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string InstalledVersion { get; set; } = string.Empty;
        public string InstalledRevision { get; set; } = string.Empty;
        public string RepositoryUrl { get; set; } = string.Empty;
        public string LatestLabel { get; set; } = string.Empty;
        public string LatestRevision { get; set; } = string.Empty;
        public string TargetReference { get; set; } = string.Empty;
        public string ChangesUrl { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public LoogaPackageUpdateStatus Status { get; set; }

        public bool IsToolkit => PackageName == "com.loogasoft.loogatoolkit";
        public bool CanUpdate =>
            Status == LoogaPackageUpdateStatus.UpdateAvailable ||
            Status == LoogaPackageUpdateStatus.UnreleasedChanges;
    }

    /// <summary>
    /// Finds Looga package updates and applies them through Unity Package Manager.
    /// </summary>
    [InitializeOnLoad]
    internal static class LoogaPackageUpdateService
    {
        private const string PackagePrefix = "com.loogasoft.";
        private const string PackageSupportMenuPath = "LoogaSoft/Package Support";
        private const string CacheDirectoryName = "LoogaSoft/PackageUpdates";
        private const string CacheFileName = "cache.json";
        private const string QueueFileName = "pending.json";
        private const double CacheHours = 24d;
        private const int GitTimeoutMilliseconds = 20000;

        private static readonly Regex LoogaDependencyPattern = new(
            "\\\"(?<name>com\\.loogasoft\\.[^\\\"]+)\\\"\\s*:\\s*\\\"(?<source>[^\\\"]+)\\\"",
            RegexOptions.Compiled);
        private static readonly Regex HashPattern = new(
            "\\\"hash\\\"\\s*:\\s*\\\"(?<hash>[0-9a-fA-F]+)\\\"",
            RegexOptions.Compiled);
        private static readonly Regex RemoteLinePattern = new(
            "^(?<hash>[0-9a-fA-F]{40})\\s+(?<reference>.+)$",
            RegexOptions.Compiled);

        private static readonly List<LoogaPackageUpdateInfo> PackageList = new();
        private static AddRequest _addRequest;
        private static PendingUpdateFile _pendingUpdates;
        private static Task<List<RemotePackageResult>> _remoteCheckTask;
        private static bool _initialized;
        private static bool _isChecking;
        private static string _operationMessage = string.Empty;

        static LoogaPackageUpdateService()
        {
            EditorApplication.delayCall += Initialize;
        }

        public static event Action Changed;

        public static IReadOnlyList<LoogaPackageUpdateInfo> Packages => PackageList;
        public static bool IsChecking => _isChecking;
        public static bool IsUpdating => _addRequest != null || _pendingUpdates?.updates?.Count > 0;
        public static string OperationMessage => _operationMessage;
        public static int AvailableUpdateCount => PackageList.Count(package => package.CanUpdate);

        public static void Initialize()
        {
            // Unity can preserve a menu checkmark set by an older package revision.
            Menu.SetChecked(PackageSupportMenuPath, false);

            if (_initialized)
                return;

            _initialized = true;
            RefreshLocalPackages();
            ApplyCache();
            ResumePendingUpdates();
            NotifyChanged();

            if (!IsCacheCurrent())
                RefreshRemote();
        }

        public static void Refresh(bool checkRemote)
        {
            Initialize();
            RefreshLocalPackages();
            ApplyCache();

            if (checkRemote)
                RefreshRemote();
            else
                NotifyChanged();
        }

        public static void UpdatePackage(LoogaPackageUpdateInfo package)
        {
            if (package == null || !package.CanUpdate || IsUpdating)
                return;

            QueueUpdates(new[] { package });
        }

        public static void UpdateAll()
        {
            if (IsUpdating)
                return;

            LoogaPackageUpdateInfo[] updates = PackageList
                .Where(package => package.CanUpdate)
                .OrderBy(package => package.IsToolkit)
                .ThenBy(package => package.DisplayName, StringComparer.Ordinal)
                .ToArray();
            QueueUpdates(updates);
        }

        public static void OpenChanges(LoogaPackageUpdateInfo package)
        {
            if (package != null && !string.IsNullOrWhiteSpace(package.ChangesUrl))
                Application.OpenURL(package.ChangesUrl);
        }

        private static void RefreshRemote()
        {
            if (_isChecking || IsUpdating || PackageList.Count == 0)
                return;

            _isChecking = true;
            _operationMessage = "Checking package repositories...";
            SetCheckingStatuses();
            NotifyChanged();

            LocalPackageSnapshot[] packages = PackageList
                .Where(package => package.Status != LoogaPackageUpdateStatus.LocalDevelopment)
                .Select(ToSnapshot)
                .ToArray();

            _remoteCheckTask = Task.Run(() => CheckRepositories(packages));
            EditorApplication.update -= PollRemoteCheck;
            EditorApplication.update += PollRemoteCheck;
        }

        private static void PollRemoteCheck()
        {
            if (_remoteCheckTask == null || !_remoteCheckTask.IsCompleted)
                return;

            EditorApplication.update -= PollRemoteCheck;
            Task<List<RemotePackageResult>> task = _remoteCheckTask;
            _remoteCheckTask = null;
            _isChecking = false;

            if (task.IsFaulted)
            {
                string message = task.Exception?.GetBaseException().Message ?? "The update check failed.";
                MarkRemoteChecksUnavailable(message);
                _operationMessage = message;
            }
            else
            {
                ApplyRemoteResults(task.Result);
                SaveCache(task.Result);
                _operationMessage = AvailableUpdateCount > 0
                    ? $"{AvailableUpdateCount} package update(s) available."
                    : "All checked packages are current.";
            }

            NotifyChanged();
        }

        private static List<RemotePackageResult> CheckRepositories(
            IReadOnlyList<LocalPackageSnapshot> packages)
        {
            List<RemotePackageResult> results = new(packages.Count);
            for (int i = 0; i < packages.Count; i++)
                results.Add(CheckRepository(packages[i]));

            return results;
        }

        private static RemotePackageResult CheckRepository(LocalPackageSnapshot package)
        {
            GitQueryResult query = QueryRemote(package.repositoryUrl);
            if (!query.success)
            {
                return new RemotePackageResult
                {
                    packageName = package.packageName,
                    error = query.error
                };
            }

            string headRevision = string.Empty;
            List<RemoteTag> tags = new();
            string[] lines = query.output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                Match match = RemoteLinePattern.Match(lines[i].Trim());
                if (!match.Success)
                    continue;

                string revision = match.Groups["hash"].Value;
                string reference = match.Groups["reference"].Value;
                if (reference == "HEAD")
                {
                    headRevision = revision;
                    continue;
                }

                const string tagPrefix = "refs/tags/";
                if (!reference.StartsWith(tagPrefix, StringComparison.Ordinal))
                    continue;

                string tag = reference.Substring(tagPrefix.Length);
                bool isPeeled = tag.EndsWith("^{}", StringComparison.Ordinal);
                if (isPeeled)
                    tag = tag.Substring(0, tag.Length - 3);

                if (SemanticVersion.TryParse(tag, out SemanticVersion version))
                {
                    int existingIndex = tags.FindIndex(existing => existing.name == tag);
                    RemoteTag candidate = new(tag, revision, version, isPeeled);
                    if (existingIndex < 0)
                        tags.Add(candidate);
                    else if (isPeeled || !tags[existingIndex].isPeeled)
                        tags[existingIndex] = candidate;
                }
            }

            RemoteTag latestTag = tags
                .OrderByDescending(tag => tag.version)
                .FirstOrDefault();
            return BuildRemoteResult(package, headRevision, latestTag);
        }

        private static RemotePackageResult BuildRemoteResult(
            LocalPackageSnapshot package,
            string headRevision,
            RemoteTag latestTag)
        {
            RemotePackageResult result = new()
            {
                packageName = package.packageName,
                repositoryUrl = package.repositoryUrl,
                headRevision = headRevision,
                checkedAtUtcTicks = DateTime.UtcNow.Ticks
            };

            bool headMatches = RevisionsMatch(package.installedRevision, headRevision);
            if (latestTag != null)
            {
                result.releaseTag = latestTag.name;
                result.releaseRevision = latestTag.revision;
            }

            bool releaseMatches = latestTag != null &&
                                  RevisionsMatch(package.installedRevision, latestTag.revision);
            bool hasNewerRelease = latestTag != null &&
                                   SemanticVersion.TryParse(
                                       package.installedVersion,
                                       out SemanticVersion installedVersion) &&
                                   latestTag.version.CompareTo(installedVersion) > 0;
            if (headMatches)
            {
                result.status = (int)LoogaPackageUpdateStatus.Current;
                result.latestLabel = latestTag?.name ?? "Default branch";
                result.latestRevision = headRevision;
                result.detail = "The installed revision matches the repository.";
            }
            else if (latestTag != null && !releaseMatches && hasNewerRelease)
            {
                result.status = (int)LoogaPackageUpdateStatus.UpdateAvailable;
                result.latestLabel = latestTag.name;
                result.latestRevision = latestTag.revision;
                result.targetReference = latestTag.name;
                result.detail = $"Release {latestTag.name} is available.";
            }
            else if (!headMatches && !string.IsNullOrEmpty(headRevision))
            {
                result.status = (int)LoogaPackageUpdateStatus.UnreleasedChanges;
                result.latestLabel = "Unreleased source";
                result.latestRevision = headRevision;
                result.targetReference = headRevision;
                result.detail = "The default branch contains changes that are not in a newer release tag.";
            }
            else if (releaseMatches && string.IsNullOrEmpty(headRevision))
            {
                result.status = (int)LoogaPackageUpdateStatus.Current;
                result.latestLabel = latestTag.name;
                result.latestRevision = latestTag.revision;
                result.detail = "The installed revision matches the latest release.";
            }
            else
            {
                result.status = (int)LoogaPackageUpdateStatus.Unavailable;
                result.latestLabel = "Repository state unavailable";
                result.latestRevision = string.Empty;
                result.detail = "Git did not return a default branch revision.";
            }

            result.changesUrl = BuildChangesUrl(
                package.repositoryUrl,
                package.installedRevision,
                result.latestRevision);
            return result;
        }

        private static GitQueryResult QueryRemote(string repositoryUrl)
        {
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = "git",
                    Arguments = $"ls-remote \"{repositoryUrl.Replace("\"", "\\\"")}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using Process process = Process.Start(startInfo);
                if (process == null)
                    return GitQueryResult.Failure("Git could not start.");

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(GitTimeoutMilliseconds))
                {
                    process.Kill();
                    return GitQueryResult.Failure("The repository check timed out.");
                }

                string output = outputTask.GetAwaiter().GetResult();
                string error = errorTask.GetAwaiter().GetResult();
                return process.ExitCode == 0
                    ? GitQueryResult.Success(output)
                    : GitQueryResult.Failure(string.IsNullOrWhiteSpace(error)
                        ? "Git could not read the repository."
                        : error.Trim());
            }
            catch (Exception exception)
            {
                return GitQueryResult.Failure(exception.GetBaseException().Message);
            }
        }

        private static void RefreshLocalPackages()
        {
            string manifestPath = Path.Combine(ProjectRoot, "Packages", "manifest.json");
            if (!File.Exists(manifestPath))
            {
                PackageList.Clear();
                return;
            }

            string manifest = File.ReadAllText(manifestPath);
            string lockContents = ReadLockFile();
            Dictionary<string, PackageManagerInfo> registeredPackages = PackageManagerInfo.GetAllRegisteredPackages()
                .Where(package => package.name.StartsWith(PackagePrefix, StringComparison.Ordinal))
                .ToDictionary(package => package.name, StringComparer.Ordinal);

            Dictionary<string, LoogaPackageUpdateInfo> previous = PackageList
                .ToDictionary(package => package.PackageName, StringComparer.Ordinal);
            PackageList.Clear();

            foreach (Match match in LoogaDependencyPattern.Matches(manifest))
            {
                string packageName = match.Groups["name"].Value;
                string source = match.Groups["source"].Value;
                registeredPackages.TryGetValue(packageName, out PackageManagerInfo packageInfo);
                previous.TryGetValue(packageName, out LoogaPackageUpdateInfo prior);

                bool isGit = IsGitSource(source);
                PackageList.Add(new LoogaPackageUpdateInfo
                {
                    PackageName = packageName,
                    DisplayName = packageInfo?.displayName ?? FormatPackageName(packageName),
                    InstalledVersion = packageInfo?.version ?? string.Empty,
                    InstalledRevision = ReadInstalledRevision(lockContents, packageName),
                    RepositoryUrl = isGit ? RemoveReference(source) : source,
                    LatestLabel = prior?.LatestLabel ?? string.Empty,
                    LatestRevision = prior?.LatestRevision ?? string.Empty,
                    TargetReference = prior?.TargetReference ?? string.Empty,
                    ChangesUrl = prior?.ChangesUrl ?? string.Empty,
                    Detail = isGit
                        ? prior?.Detail ?? "The repository has not been checked."
                        : "This package uses a local development source.",
                    Status = isGit
                        ? prior?.Status ?? LoogaPackageUpdateStatus.Checking
                        : LoogaPackageUpdateStatus.LocalDevelopment
                });
            }

            PackageList.Sort((left, right) =>
                string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
        }

        private static void ApplyCache()
        {
            CacheFile cache = LoadJson<CacheFile>(CachePath);
            if (cache?.entries == null)
                return;

            Dictionary<string, RemotePackageResult> entries = cache.entries
                .ToDictionary(entry => entry.packageName, StringComparer.Ordinal);
            for (int i = 0; i < PackageList.Count; i++)
            {
                LoogaPackageUpdateInfo package = PackageList[i];
                if (package.Status == LoogaPackageUpdateStatus.LocalDevelopment ||
                    !entries.TryGetValue(package.PackageName, out RemotePackageResult result))
                {
                    continue;
                }

                ApplyRemoteResult(package, result);
            }
        }

        private static void ApplyRemoteResults(IReadOnlyList<RemotePackageResult> results)
        {
            Dictionary<string, RemotePackageResult> byName = results
                .ToDictionary(result => result.packageName, StringComparer.Ordinal);
            for (int i = 0; i < PackageList.Count; i++)
            {
                LoogaPackageUpdateInfo package = PackageList[i];
                if (byName.TryGetValue(package.PackageName, out RemotePackageResult result))
                    ApplyRemoteResult(package, result);
            }
        }

        private static void ApplyRemoteResult(
            LoogaPackageUpdateInfo package,
            RemotePackageResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.error))
            {
                package.Status = LoogaPackageUpdateStatus.Unavailable;
                package.Detail = result.error;
                return;
            }

            LocalPackageSnapshot snapshot = ToSnapshot(package);
            RemoteTag release = null;
            if (!string.IsNullOrWhiteSpace(result.releaseTag) &&
                SemanticVersion.TryParse(result.releaseTag, out SemanticVersion version))
            {
                release = new RemoteTag(result.releaseTag, result.releaseRevision, version);
            }

            RemotePackageResult current = BuildRemoteResult(snapshot, result.headRevision, release);
            package.Status = (LoogaPackageUpdateStatus)current.status;
            package.LatestLabel = current.latestLabel;
            package.LatestRevision = current.latestRevision;
            package.TargetReference = current.targetReference;
            package.ChangesUrl = current.changesUrl;
            package.Detail = current.detail;
        }

        private static void SaveCache(List<RemotePackageResult> entries)
        {
            CacheFile cache = new()
            {
                checkedAtUtcTicks = DateTime.UtcNow.Ticks,
                entries = entries
            };
            SaveJson(CachePath, cache);
        }

        private static bool IsCacheCurrent()
        {
            CacheFile cache = LoadJson<CacheFile>(CachePath);
            if (cache == null || cache.checkedAtUtcTicks <= 0)
                return false;

            DateTime checkedAt = new(cache.checkedAtUtcTicks, DateTimeKind.Utc);
            return DateTime.UtcNow - checkedAt < TimeSpan.FromHours(CacheHours);
        }

        private static void QueueUpdates(IEnumerable<LoogaPackageUpdateInfo> packages)
        {
            List<PendingUpdate> updates = packages
                .Where(package => package.CanUpdate && !string.IsNullOrWhiteSpace(package.TargetReference))
                .Select(package => new PendingUpdate
                {
                    packageName = package.PackageName,
                    displayName = package.DisplayName,
                    packageIdentifier = $"{package.RepositoryUrl}#{package.TargetReference}",
                    targetRevision = package.LatestRevision
                })
                .ToList();
            if (updates.Count == 0)
                return;

            _pendingUpdates = new PendingUpdateFile { updates = updates };
            SaveJson(QueuePath, _pendingUpdates);
            StartNextUpdate();
        }

        private static void ResumePendingUpdates()
        {
            _pendingUpdates = LoadJson<PendingUpdateFile>(QueuePath);
            if (_pendingUpdates?.updates == null || _pendingUpdates.updates.Count == 0)
                return;

            string manifestPath = Path.Combine(ProjectRoot, "Packages", "manifest.json");
            string manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : string.Empty;
            string lockContents = ReadLockFile();
            _pendingUpdates.updates.RemoveAll(update =>
                IsAlreadyInstalled(manifest, lockContents, update));
            SaveJson(QueuePath, _pendingUpdates);
            StartNextUpdate();
        }

        private static bool IsAlreadyInstalled(
            string manifest,
            string lockContents,
            PendingUpdate update)
        {
            string installedRevision = ReadInstalledRevision(lockContents, update.packageName);
            if (!string.IsNullOrWhiteSpace(update.targetRevision) &&
                string.Equals(installedRevision, update.targetRevision, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string dependency = $"\"{update.packageName}\"";
            int dependencyIndex = manifest.IndexOf(dependency, StringComparison.Ordinal);
            if (dependencyIndex < 0)
                return false;

            int valueIndex = manifest.IndexOf(
                $"\"{update.packageIdentifier}\"",
                dependencyIndex + dependency.Length,
                StringComparison.Ordinal);
            if (valueIndex < 0)
                return false;

            int nextEntry = manifest.IndexOf(',', dependencyIndex + dependency.Length);
            return nextEntry < 0 || valueIndex < nextEntry;
        }

        private static void StartNextUpdate()
        {
            if (_addRequest != null)
                return;

            if (_pendingUpdates?.updates == null || _pendingUpdates.updates.Count == 0)
            {
                CompleteUpdateQueue();
                return;
            }

            PendingUpdate update = _pendingUpdates.updates[0];
            _operationMessage = $"Updating {update.displayName}...";
            _addRequest = Client.Add(update.packageIdentifier);
            EditorApplication.update += PollAddRequest;
            NotifyChanged();
        }

        private static void PollAddRequest()
        {
            if (_addRequest == null || !_addRequest.IsCompleted)
                return;

            EditorApplication.update -= PollAddRequest;
            if (_addRequest.Status == StatusCode.Success)
            {
                PendingUpdate completed = _pendingUpdates.updates[0];
                _pendingUpdates.updates.RemoveAt(0);
                SaveJson(QueuePath, _pendingUpdates);
                _operationMessage = $"Updated {completed.displayName}.";
                _addRequest = null;
                StartNextUpdate();
                return;
            }

            string error = _addRequest.Error?.message ?? "Unity Package Manager rejected the update.";
            _operationMessage = $"Package update failed: {error}";
            _addRequest = null;
            _pendingUpdates = null;
            DeleteFile(QueuePath);
            NotifyChanged();
        }

        private static void CompleteUpdateQueue()
        {
            _addRequest = null;
            _pendingUpdates = null;
            DeleteFile(QueuePath);
            _operationMessage = "Package updates completed.";
            RefreshLocalPackages();
            ApplyCache();
            NotifyChanged();
        }

        private static void SetCheckingStatuses()
        {
            for (int i = 0; i < PackageList.Count; i++)
            {
                if (PackageList[i].Status != LoogaPackageUpdateStatus.LocalDevelopment)
                    PackageList[i].Status = LoogaPackageUpdateStatus.Checking;
            }
        }

        private static void MarkRemoteChecksUnavailable(string message)
        {
            for (int i = 0; i < PackageList.Count; i++)
            {
                LoogaPackageUpdateInfo package = PackageList[i];
                if (package.Status == LoogaPackageUpdateStatus.LocalDevelopment)
                    continue;

                package.Status = LoogaPackageUpdateStatus.Unavailable;
                package.Detail = message;
            }
        }

        private static LocalPackageSnapshot ToSnapshot(LoogaPackageUpdateInfo package)
        {
            return new LocalPackageSnapshot
            {
                packageName = package.PackageName,
                installedVersion = package.InstalledVersion,
                installedRevision = package.InstalledRevision,
                repositoryUrl = package.RepositoryUrl
            };
        }

        private static string ReadLockFile()
        {
            string path = Path.Combine(ProjectRoot, "Packages", "packages-lock.json");
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static string ReadInstalledRevision(string lockContents, string packageName)
        {
            string key = $"\"{packageName}\"";
            int searchIndex = 0;
            int objectStart = -1;
            while (searchIndex < lockContents.Length)
            {
                int keyIndex = lockContents.IndexOf(key, searchIndex, StringComparison.Ordinal);
                if (keyIndex < 0)
                    return string.Empty;

                int colonIndex = lockContents.IndexOf(':', keyIndex + key.Length);
                if (colonIndex < 0)
                    return string.Empty;

                int valueIndex = colonIndex + 1;
                while (valueIndex < lockContents.Length && char.IsWhiteSpace(lockContents[valueIndex]))
                    valueIndex++;

                if (valueIndex < lockContents.Length && lockContents[valueIndex] == '{')
                {
                    objectStart = valueIndex;
                    break;
                }

                searchIndex = keyIndex + key.Length;
            }

            if (objectStart < 0)
                return string.Empty;

            int depth = 0;
            for (int i = objectStart; i < lockContents.Length; i++)
            {
                if (lockContents[i] == '{')
                    depth++;
                else if (lockContents[i] == '}')
                    depth--;

                if (depth != 0)
                    continue;

                string packageObject = lockContents.Substring(objectStart, i - objectStart + 1);
                Match hash = HashPattern.Match(packageObject);
                return hash.Success ? hash.Groups["hash"].Value : string.Empty;
            }

            return string.Empty;
        }

        private static string FormatPackageName(string packageName)
        {
            string shortName = packageName.StartsWith(PackagePrefix, StringComparison.Ordinal)
                ? packageName.Substring(PackagePrefix.Length)
                : packageName;
            string[] words = shortName.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            }

            return $"Looga {string.Join(" ", words)}";
        }

        private static bool IsGitSource(string source)
        {
            return source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   source.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   source.StartsWith("git://", StringComparison.OrdinalIgnoreCase) ||
                   source.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) ||
                   source.StartsWith("git@", StringComparison.OrdinalIgnoreCase);
        }

        private static string RemoveReference(string source)
        {
            int referenceIndex = source.LastIndexOf('#');
            return referenceIndex >= 0 ? source.Substring(0, referenceIndex) : source;
        }

        private static bool RevisionsMatch(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return left.StartsWith(right, StringComparison.OrdinalIgnoreCase) ||
                   right.StartsWith(left, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildChangesUrl(
            string repositoryUrl,
            string installedRevision,
            string latestRevision)
        {
            string browserUrl = repositoryUrl;
            if (browserUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                browserUrl = browserUrl.Substring(0, browserUrl.Length - 4);

            if (string.IsNullOrWhiteSpace(installedRevision) || string.IsNullOrWhiteSpace(latestRevision))
                return browserUrl;

            return $"{browserUrl}/compare/{installedRevision}...{latestRevision}";
        }

        private static T LoadJson<T>(string path) where T : class
        {
            try
            {
                return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void SaveJson<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? CacheDirectory);
            File.WriteAllText(path, JsonUtility.ToJson(value, true));
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
        private static string CacheDirectory => Path.Combine(ProjectRoot, "Library", CacheDirectoryName);
        private static string CachePath => Path.Combine(CacheDirectory, CacheFileName);
        private static string QueuePath => Path.Combine(CacheDirectory, QueueFileName);

        [Serializable]
        private sealed class CacheFile
        {
            public long checkedAtUtcTicks;
            public List<RemotePackageResult> entries = new();
        }

        [Serializable]
        private sealed class PendingUpdateFile
        {
            public List<PendingUpdate> updates = new();
        }

        [Serializable]
        private sealed class PendingUpdate
        {
            public string packageName = string.Empty;
            public string displayName = string.Empty;
            public string packageIdentifier = string.Empty;
            public string targetRevision = string.Empty;
        }

        [Serializable]
        private sealed class RemotePackageResult
        {
            public string packageName = string.Empty;
            public string repositoryUrl = string.Empty;
            public string headRevision = string.Empty;
            public string releaseTag = string.Empty;
            public string releaseRevision = string.Empty;
            public string latestLabel = string.Empty;
            public string latestRevision = string.Empty;
            public string targetReference = string.Empty;
            public string changesUrl = string.Empty;
            public string detail = string.Empty;
            public string error = string.Empty;
            public int status;
            public long checkedAtUtcTicks;
        }

        private sealed class LocalPackageSnapshot
        {
            public string packageName = string.Empty;
            public string installedVersion = string.Empty;
            public string installedRevision = string.Empty;
            public string repositoryUrl = string.Empty;
        }

        private sealed class RemoteTag
        {
            public RemoteTag(
                string name,
                string revision,
                SemanticVersion version,
                bool isPeeled = false)
            {
                this.name = name;
                this.revision = revision;
                this.version = version;
                this.isPeeled = isPeeled;
            }

            public string name { get; }
            public string revision { get; }
            public SemanticVersion version { get; }
            public bool isPeeled { get; }
        }

        private readonly struct GitQueryResult
        {
            private GitQueryResult(bool success, string output, string error)
            {
                this.success = success;
                this.output = output;
                this.error = error;
            }

            public readonly bool success;
            public readonly string output;
            public readonly string error;

            public static GitQueryResult Success(string output) => new(true, output, string.Empty);
            public static GitQueryResult Failure(string error) => new(false, string.Empty, error);
        }

        private readonly struct SemanticVersion : IComparable<SemanticVersion>
        {
            private SemanticVersion(int major, int minor, int patch)
            {
                _major = major;
                _minor = minor;
                _patch = patch;
            }

            private readonly int _major;
            private readonly int _minor;
            private readonly int _patch;

            public int CompareTo(SemanticVersion other)
            {
                int majorComparison = _major.CompareTo(other._major);
                if (majorComparison != 0)
                    return majorComparison;

                int minorComparison = _minor.CompareTo(other._minor);
                return minorComparison != 0 ? minorComparison : _patch.CompareTo(other._patch);
            }

            public static bool TryParse(string tag, out SemanticVersion version)
            {
                version = default;
                if (string.IsNullOrWhiteSpace(tag))
                    return false;

                string value = tag.TrimStart('v', 'V');
                if (value.IndexOf('-') >= 0)
                    return false;

                string[] parts = value.Split('.');
                if (parts.Length < 2 || parts.Length > 3 ||
                    !int.TryParse(parts[0], out int major) ||
                    !int.TryParse(parts[1], out int minor))
                {
                    return false;
                }

                int patch = 0;
                if (parts.Length > 2 && !int.TryParse(parts[2], out patch))
                    return false;

                version = new SemanticVersion(major, minor, patch);
                return true;
            }
        }
    }
}
