using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using StrandedDeepModManager.Content;

namespace StrandedDeepModManager
{
    public sealed class ManagerEngine
    {
        public string GameRoot { get; private set; }
        public string CatalogUrl { get; private set; }
        public string DataRoot { get; private set; }
        public string CatalogStatus { get; private set; }
        public bool CatalogLoadedFromCache { get; private set; }

        public CatalogRoot Catalog { get; private set; }
        public PageCache PageCache { get; private set; }
        public ModPageService ModPages { get; private set; }

        public ManagerEngine(string gameRoot, string catalogUrl)
        {
            if (String.IsNullOrWhiteSpace(gameRoot))
                throw new InvalidOperationException("Select the Stranded Deep game directory first.");

            GameRoot = Path.GetFullPath(gameRoot);
            CatalogUrl = String.IsNullOrWhiteSpace(catalogUrl) ? AppInfo.CatalogUrl : catalogUrl.Trim();
            DataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BamEx",
                "StrandedDeepModManager");

            PageCache = new PageCache(DataRoot);
            ModPages = new ModPageService(PageCache);
        }

        public void LoadCatalog()
        {
            if (!GameLocator.LooksLikeGameRoot(GameRoot))
                throw new InvalidOperationException("Stranded Deep game directory not found or invalid: " + GameRoot);

            Directory.CreateDirectory(DataRoot);
            Directory.CreateDirectory(CacheRoot);

            string catalogText = LoadVerifiedCatalogText();
            Catalog = JsonUtil.Deserialize<CatalogRoot>(catalogText);

            if (Catalog == null || Catalog.schemaVersion != 1)
                throw new InvalidOperationException("Unsupported or invalid catalog schema.");

            if (Catalog.game == null || !String.Equals(Catalog.game.id, "stranded-deep", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Catalog is not for Stranded Deep.");

            if (Catalog.packages == null)
                Catalog.packages = new List<CatalogPackage>();
        }

        public IList<PackageStatus> ScanAll(bool adoptExactMatches)
        {
            EnsureCatalog();

            List<PackageStatus> result = new List<PackageStatus>();

            foreach (CatalogPackage package in Catalog.packages.OrderBy(p => p.name))
            {
                result.Add(ScanPackage(package, adoptExactMatches));
            }

            return result;
        }

        public PackageStatus ScanPackage(CatalogPackage package, bool adoptExactMatch)
        {
            PackageStatus status = new PackageStatus();
            status.CatalogPackage = package;
            status.Kind = PackageStatusKind.Error;
            status.Detail = "";

            try
            {
                InstalledState state = ReadInstalledState(package.id);

                if (state != null)
                {
                    status.InstalledVersion = state.version;

                    bool stateFilesMatch = InstalledStateFilesMatch(state);
                    bool sameVersion = package.latest != null &&
                        String.Equals(state.version, package.latest.version, StringComparison.OrdinalIgnoreCase);

                    if (!stateFilesMatch)
                    {
                        if (sameVersion)
                        {
                            string reconcileFailure;
                            if (TryReconcileInstalledStateWithCurrentRelease(
                                package,
                                status,
                                adoptExactMatch,
                                out reconcileFailure))
                            {
                                return status;
                            }

                            status.Kind = PackageStatusKind.Modified;
                            status.Detail = "Files differ from manager state and do not exactly match the current public release." +
                                (String.IsNullOrWhiteSpace(reconcileFailure) ? "" : " " + reconcileFailure);
                            return status;
                        }

                        status.Kind = PackageStatusKind.Modified;
                        status.Detail = "Files differ from manager state.";
                        return status;
                    }

                    if (package.latest != null && !sameVersion)
                    {
                        status.Kind = PackageStatusKind.UpdateAvailable;
                        status.Detail = "Installed " + state.version + ", available " + package.latest.version + ".";
                        return status;
                    }

                    if (sameVersion && !PackageShaMatchesCatalog(state, package))
                    {
                        string reconcileFailure;
                        if (TryReconcileInstalledStateWithCurrentRelease(
                            package,
                            status,
                            adoptExactMatch,
                            out reconcileFailure))
                        {
                            return status;
                        }

                        status.Kind = PackageStatusKind.DifferentBuild;
                        status.Detail = "Installed files belong to a different build of version " + state.version +
                            " than the current public release." +
                            (String.IsNullOrWhiteSpace(reconcileFailure) ? "" : " " + reconcileFailure);
                        return status;
                    }

                    status.Kind = PackageStatusKind.Installed;
                    status.Detail = "Installed and verified.";
                    return status;
                }

                string zip = LocatePackageZip(package);
                status.PackageZipPath = zip;

                if (String.IsNullOrEmpty(zip) || !File.Exists(zip))
                {
                    status.Kind = PackageStatusKind.PackageMissing;
                    status.Detail = "Release ZIP is not available.";
                    return status;
                }

                PackageInspection inspection = InspectAndVerifyPackage(package, zip, false);
                status.LatestManifest = inspection.Manifest;

                bool anyOwnedPathExists = AnyOwnedPathExists(inspection.Manifest.ownedPaths);
                bool exactPayloadMatch = PackagePayloadMatchesGame(inspection.Files, inspection.Manifest.ownedPaths);

                if (exactPayloadMatch)
                {
                    status.Kind = PackageStatusKind.Installed;
                    status.InstalledVersion = inspection.Manifest.version;
                    status.Detail = adoptExactMatch
                        ? "Existing manual install adopted."
                        : "Existing files match release.";

                    if (adoptExactMatch)
                    {
                        WriteInstalledState(CreateInstalledState(package, inspection));
                    }

                    return status;
                }

                if (anyOwnedPathExists)
                {
                    status.Kind = PackageStatusKind.Modified;
                    status.Detail = "Owned runtime path exists but does not match current release.";
                    return status;
                }

                status.Kind = PackageStatusKind.NotInstalled;
                status.Detail = "Not installed.";
                return status;
            }
            catch (Exception ex)
            {
                status.Kind = PackageStatusKind.Error;
                status.Detail = ex.Message;
                return status;
            }
        }

        public void InstallOrUpdate(CatalogPackage package)
        {
            EnsureGameNotRunning();

            string zip = LocatePackageZip(package);
            if (String.IsNullOrEmpty(zip))
                throw new InvalidOperationException("Release ZIP not found for " + package.id);

            PackageInspection inspection = InspectAndVerifyPackage(package, zip, true);
            CheckDependencies(inspection.Manifest);

            InstalledState previousState = ReadInstalledState(package.id);
            List<string> removalPaths = previousState != null && previousState.ownedPaths != null
                ? previousState.ownedPaths
                : inspection.Manifest.ownedPaths;

            string backupRoot = null;
            bool removalStarted = false;
            bool installCopyStarted = false;

            try
            {
                bool isUpdate = previousState != null || AnyOwnedPathExists(removalPaths);

                if (isUpdate)
                {
                    backupRoot = CreateBackup(package.id, removalPaths, inspection.Manifest.persistence);
                }

                removalStarted = true;
                RemoveOwnedPaths(removalPaths);

                string temp = ExtractPackageSafely(zip);
                try
                {
                    installCopyStarted = true;
                    CopyInstallPayload(temp);
                }
                finally
                {
                    TryDeleteDirectory(temp);
                }

                WriteInstalledState(CreateInstalledState(package, inspection));
            }
            catch
            {
                if (installCopyStarted)
                {
                    try
                    {
                        RemoveOwnedPaths(inspection.Manifest.ownedPaths);
                    }
                    catch
                    {
                    }
                }

                if (removalStarted && !String.IsNullOrEmpty(backupRoot))
                {
                    RestoreOwnedPathsFromBackup(backupRoot);
                }

                throw;
            }
        }

        public void Uninstall(CatalogPackage package)
        {
            EnsureGameNotRunning();

            InstalledState state = ReadInstalledState(package.id);
            List<string> ownedPaths = null;
            PersistenceSpec persistence = null;

            if (state != null)
            {
                ownedPaths = state.ownedPaths;
                persistence = state.persistence;
            }
            else
            {
                string zip = LocatePackageZip(package);
                if (String.IsNullOrEmpty(zip))
                    throw new InvalidOperationException("Cannot safely uninstall: no manager state and release ZIP is unavailable.");

                PackageInspection inspection = InspectAndVerifyPackage(package, zip, false);
                ownedPaths = inspection.Manifest.ownedPaths;
                persistence = inspection.Manifest.persistence;
            }

            string backupRoot = CreateBackup(package.id, ownedPaths, persistence);

            try
            {
                RemoveOwnedPaths(ownedPaths);
                DeleteInstalledState(package.id);
            }
            catch
            {
                RestoreOwnedPathsFromBackup(backupRoot);
                throw;
            }
        }

        public string LocatePackageZip(CatalogPackage package)
        {
            if (package == null || package.latest == null || package.latest.download == null)
                return null;

            string asset = package.latest.download.asset;
            string version = package.latest.version;
            string tag = package.latest.tag;
            string repository = package.repository;
            string expectedSha = package.latest.download.sha256 == null
                ? ""
                : package.latest.download.sha256.Trim().ToLowerInvariant();

            if (String.IsNullOrWhiteSpace(asset) ||
                String.IsNullOrWhiteSpace(version) ||
                String.IsNullOrWhiteSpace(tag) ||
                String.IsNullOrWhiteSpace(repository) ||
                String.IsNullOrWhiteSpace(expectedSha))
            {
                return null;
            }

            if (!String.Equals(package.latest.download.provider, "github-release", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unsupported download provider for " + package.id + ".");

            ValidateRepositoryName(repository);
            ValidateFileName(asset);

            string packageDir = Path.Combine(CacheRoot, "packages", SafeFileName(package.id), SafeFileName(version));
            Directory.CreateDirectory(packageDir);

            string cached = Path.Combine(packageDir, asset);

            if (File.Exists(cached))
            {
                if (String.Equals(Sha256File(cached), expectedSha, StringComparison.OrdinalIgnoreCase))
                    return cached;

                File.Delete(cached);
            }

            string url =
                "https://github.com/" + AppInfo.GitHubOwner + "/" + repository +
                "/releases/download/" + Uri.EscapeDataString(tag) + "/" + Uri.EscapeDataString(asset);

            DownloadFileVerified(url, cached, expectedSha);
            return cached;
        }

        public PackageInspection InspectAndVerifyPackage(CatalogPackage package, string zipPath, bool verifyDependenciesLater)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("Package ZIP not found.", zipPath);

            string actualZipHash = Sha256File(zipPath);
            string expectedZipHash = package.latest.download.sha256 == null
                ? ""
                : package.latest.download.sha256.Trim().ToLowerInvariant();

            if (!String.Equals(actualZipHash, expectedZipHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ZIP SHA-256 mismatch for " + package.id);

            string temp = ExtractPackageSafely(zipPath);

            try
            {
                string manifestPath = Path.Combine(temp, "manifest.json");
                string filesPath = Path.Combine(temp, "files.json");

                if (!File.Exists(manifestPath))
                    throw new InvalidOperationException("manifest.json is missing.");

                if (!File.Exists(filesPath))
                    throw new InvalidOperationException("files.json is missing.");

                ManifestRoot manifest = JsonUtil.ReadFile<ManifestRoot>(manifestPath);
                FilesManifestRoot files = JsonUtil.ReadFile<FilesManifestRoot>(filesPath);

                if (manifest == null || manifest.schemaVersion != 1)
                    throw new InvalidOperationException("Invalid manifest schema.");

                if (!String.Equals(manifest.id, package.id, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Package id does not match catalog.");

                if (!String.Equals(manifest.version, package.latest.version, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Package version does not match catalog.");

                if (manifest.ownedPaths == null || manifest.ownedPaths.Count == 0)
                    throw new InvalidOperationException("Package has no ownedPaths.");

                foreach (string ownedPath in manifest.ownedPaths)
                    ValidateOwnedPath(ownedPath);

                if (files == null || files.files == null)
                    throw new InvalidOperationException("Invalid files.json.");

                VerifyExtractedFiles(temp, files);
                VerifyPayloadOwnership(files, manifest.ownedPaths);

                PackageInspection inspection = new PackageInspection();
                inspection.ZipPath = zipPath;
                inspection.Manifest = manifest;
                inspection.Files = files;
                return inspection;
            }
            finally
            {
                TryDeleteDirectory(temp);
            }
        }

        private void VerifyExtractedFiles(string root, FilesManifestRoot files)
        {
            HashSet<string> expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (PackageFileEntry entry in files.files)
            {
                string relative = NormalizeRelative(entry.path);
                if (!expected.Add(relative))
                    throw new InvalidOperationException("Duplicate file in files.json: " + relative);

                string full = SafeCombine(root, relative);
                if (!File.Exists(full))
                    throw new InvalidOperationException("Package file missing: " + relative);

                FileInfo info = new FileInfo(full);
                if (info.Length != entry.size)
                    throw new InvalidOperationException("Size mismatch: " + relative);

                string actualHash = Sha256File(full);
                if (!String.Equals(actualHash, entry.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("SHA-256 mismatch: " + relative);
            }

            foreach (string full in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string relative = GetRelativePath(root, full).Replace('\\', '/');
                if (String.Equals(relative, "files.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!expected.Contains(relative))
                    throw new InvalidOperationException("Unlisted file in package: " + relative);
            }
        }

        private bool PackagePayloadMatchesGame(FilesManifestRoot files, IList<string> ownedPaths)
        {
            if (files == null || files.files == null || ownedPaths == null || ownedPaths.Count == 0)
                return false;

            bool hasPayload = false;
            HashSet<string> expectedRuntimeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (PackageFileEntry entry in files.files)
            {
                string relative = NormalizeRelative(entry.path);

                if (!relative.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase))
                    continue;

                hasPayload = true;
                expectedRuntimeFiles.Add(relative);

                string live = SafeCombine(GameRoot, relative);

                if (!File.Exists(live))
                    return false;

                FileInfo info = new FileInfo(live);
                if (info.Length != entry.size)
                    return false;

                if (!String.Equals(Sha256File(live), entry.sha256, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!hasPayload)
                return false;

            foreach (string ownedPath in ownedPaths)
            {
                ValidateOwnedPath(ownedPath);
                string normalizedOwned = NormalizeRelative(ownedPath);
                string liveOwned = SafeCombine(GameRoot, normalizedOwned);

                if (File.Exists(liveOwned))
                {
                    if (!expectedRuntimeFiles.Contains(normalizedOwned))
                        return false;
                    continue;
                }

                if (!Directory.Exists(liveOwned))
                    continue;

                foreach (string liveFile in Directory.GetFiles(liveOwned, "*", SearchOption.AllDirectories))
                {
                    string relative = GetRelativePath(GameRoot, liveFile).Replace('\\', '/');
                    if (!expectedRuntimeFiles.Contains(relative))
                        return false;
                }
            }

            return true;
        }

        private static bool PackageShaMatchesCatalog(InstalledState state, CatalogPackage package)
        {
            if (state == null || package == null || package.latest == null || package.latest.download == null)
                return false;

            string stateSha = state.packageSha256 == null ? "" : state.packageSha256.Trim();
            string catalogSha = package.latest.download.sha256 == null ? "" : package.latest.download.sha256.Trim();

            if (stateSha.Length == 0 || catalogSha.Length == 0)
                return false;

            return String.Equals(stateSha, catalogSha, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryReconcileInstalledStateWithCurrentRelease(
            CatalogPackage package,
            PackageStatus status,
            bool adoptExactMatch,
            out string failureDetail)
        {
            failureDetail = null;

            try
            {
                string zip = LocatePackageZip(package);
                status.PackageZipPath = zip;

                if (String.IsNullOrEmpty(zip) || !File.Exists(zip))
                {
                    failureDetail = "Current public release ZIP is unavailable for verification.";
                    return false;
                }

                PackageInspection inspection = InspectAndVerifyPackage(package, zip, false);
                status.LatestManifest = inspection.Manifest;

                if (!PackagePayloadMatchesGame(inspection.Files, inspection.Manifest.ownedPaths))
                    return false;

                status.Kind = PackageStatusKind.Installed;
                status.InstalledVersion = inspection.Manifest.version;
                status.Detail = adoptExactMatch
                    ? "Installed files exactly match the current public release; manager state refreshed."
                    : "Installed files exactly match the current public release.";

                if (adoptExactMatch)
                    WriteInstalledState(CreateInstalledState(package, inspection));

                return true;
            }
            catch (Exception ex)
            {
                failureDetail = "Public release verification failed: " + ex.Message;
                return false;
            }
        }

        private bool InstalledStateFilesMatch(InstalledState state)
        {
            if (state.files == null || state.files.Count == 0)
                return false;

            foreach (PackageFileEntry entry in state.files)
            {
                string relative = NormalizeRelative(entry.path);

                if (!relative.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase))
                    continue;

                string live = SafeCombine(GameRoot, relative);
                if (!File.Exists(live))
                    return false;

                FileInfo info = new FileInfo(live);
                if (info.Length != entry.size)
                    return false;

                if (!String.Equals(Sha256File(live), entry.sha256, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private InstalledState CreateInstalledState(CatalogPackage package, PackageInspection inspection)
        {
            InstalledState state = new InstalledState();
            state.schemaVersion = 1;
            state.id = inspection.Manifest.id;
            state.name = inspection.Manifest.name;
            state.version = inspection.Manifest.version;
            state.packageSha256 = package.latest.download.sha256;
            state.installedAt = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture);
            state.ownedPaths = inspection.Manifest.ownedPaths == null
                ? new List<string>()
                : new List<string>(inspection.Manifest.ownedPaths);
            state.persistence = inspection.Manifest.persistence;

            state.files = new List<PackageFileEntry>();
            foreach (PackageFileEntry entry in inspection.Files.files)
            {
                string relative = NormalizeRelative(entry.path);
                if (relative.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase))
                {
                    PackageFileEntry copy = new PackageFileEntry();
                    copy.path = relative;
                    copy.sha256 = entry.sha256;
                    copy.size = entry.size;
                    state.files.Add(copy);
                }
            }

            return state;
        }

        private void CheckDependencies(ManifestRoot manifest)
        {
            if (manifest.dependencies == null)
                return;

            foreach (DependencySpec dep in manifest.dependencies)
            {
                if (dep == null || !dep.required)
                    continue;

                if (String.Equals(dep.id, "bepinex", StringComparison.OrdinalIgnoreCase))
                {
                    string bepinex = Path.Combine(GameRoot, "BepInEx", "core", "BepInEx.dll");
                    if (!File.Exists(bepinex))
                        throw new InvalidOperationException("Required dependency BepInEx is not installed.");
                    continue;
                }

                CatalogPackage depPackage = FindCatalogPackage(dep.id);
                if (depPackage == null)
                    throw new InvalidOperationException("Required dependency is absent from catalog: " + dep.id);

                PackageStatus depStatus = ScanPackage(depPackage, false);
                if (depStatus.Kind != PackageStatusKind.Installed &&
                    depStatus.Kind != PackageStatusKind.UpdateAvailable)
                {
                    throw new InvalidOperationException("Required dependency is not installed: " + dep.id);
                }

                if (!String.IsNullOrWhiteSpace(dep.minimumVersion) &&
                    !VersionAtLeast(depStatus.InstalledVersion, dep.minimumVersion))
                {
                    throw new InvalidOperationException(
                        "Dependency " + dep.id + " must be at least " + dep.minimumVersion +
                        ", installed " + depStatus.InstalledVersion + ".");
                }
            }
        }

        private CatalogPackage FindCatalogPackage(string id)
        {
            if (Catalog == null || Catalog.packages == null)
                return null;

            return Catalog.packages.FirstOrDefault(
                p => String.Equals(p.id, id, StringComparison.OrdinalIgnoreCase));
        }

        private static bool VersionAtLeast(string installed, string minimum)
        {
            Version a;
            Version b;

            if (Version.TryParse(installed, out a) && Version.TryParse(minimum, out b))
                return a.CompareTo(b) >= 0;

            return String.Equals(installed, minimum, StringComparison.OrdinalIgnoreCase);
        }

        private string CreateBackup(string packageId, IList<string> ownedPaths, PersistenceSpec persistence)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string backupRoot = Path.Combine(
                DataRoot,
                "backups",
                timestamp + "-" + SafeFileName(packageId));

            Directory.CreateDirectory(backupRoot);

            string ownedRoot = Path.Combine(backupRoot, "owned");

            if (ownedPaths != null)
            {
                foreach (string relative in ownedPaths)
                {
                    string safe = NormalizeRelative(relative);
                    string live = SafeCombine(GameRoot, safe);

                    if (Directory.Exists(live))
                    {
                        CopyDirectory(live, SafeCombine(ownedRoot, safe));
                    }
                    else if (File.Exists(live))
                    {
                        string dst = SafeCombine(ownedRoot, safe);
                        Directory.CreateDirectory(Path.GetDirectoryName(dst));
                        File.Copy(live, dst, true);
                    }
                }
            }

            if (persistence != null && persistence.backupBeforeUpdate)
            {
                string config = Path.Combine(GameRoot, "BepInEx", "config");
                if ((persistence.usesBepInExConfig || persistence.usesSidecarData) &&
                    Directory.Exists(config))
                {
                    CopyDirectoryLongPathSafe(
                        config,
                        Path.Combine(backupRoot, "BepInEx-config"));
                }

                string saveData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData",
                    "LocalLow",
                    "Beam Team Games",
                    "Stranded Deep",
                    "Data");

                if (persistence.usesGameSave && Directory.Exists(saveData))
                {
                    CopyDirectoryLongPathSafe(
                        saveData,
                        Path.Combine(backupRoot, "Save-Data"));
                }
            }

            return backupRoot;
        }

        private void RestoreOwnedPathsFromBackup(string backupRoot)
        {
            string ownedRoot = Path.Combine(backupRoot, "owned");
            if (!Directory.Exists(ownedRoot))
                return;

            foreach (string sourceFile in Directory.GetFiles(ownedRoot, "*", SearchOption.AllDirectories))
            {
                string relative = GetRelativePath(ownedRoot, sourceFile);
                string destination = SafeCombine(GameRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(sourceFile, destination, true);
            }
        }

        private void RemoveOwnedPaths(IList<string> ownedPaths)
        {
            if (ownedPaths == null)
                return;

            foreach (string relative in ownedPaths)
            {
                ValidateOwnedPath(relative);
                string safe = NormalizeRelative(relative);
                string live = SafeCombine(GameRoot, safe);

                if (Directory.Exists(live))
                    Directory.Delete(live, true);
                else if (File.Exists(live))
                    File.Delete(live);
            }
        }

        private void CopyInstallPayload(string extractedRoot)
        {
            string bepinex = Path.Combine(extractedRoot, "BepInEx");
            if (!Directory.Exists(bepinex))
                throw new InvalidOperationException("Package contains no BepInEx payload.");

            foreach (string sourceFile in Directory.GetFiles(bepinex, "*", SearchOption.AllDirectories))
            {
                string relativeInsideBepInEx = GetRelativePath(bepinex, sourceFile);
                string destination = SafeCombine(
                    GameRoot,
                    Path.Combine("BepInEx", relativeInsideBepInEx));

                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(sourceFile, destination, true);
            }
        }

        private bool AnyOwnedPathExists(IList<string> ownedPaths)
        {
            if (ownedPaths == null)
                return false;

            foreach (string relative in ownedPaths)
            {
                string live = SafeCombine(GameRoot, NormalizeRelative(relative));
                if (Directory.Exists(live) || File.Exists(live))
                    return true;
            }

            return false;
        }

        private string CacheRoot
        {
            get { return Path.Combine(DataRoot, "cache"); }
        }

        private string CatalogCachePath
        {
            get { return Path.Combine(CacheRoot, "catalog", "catalog.json"); }
        }

        private string CatalogCacheShaPath
        {
            get { return Path.Combine(CacheRoot, "catalog", "catalog.json.sha256"); }
        }

        private string LoadVerifiedCatalogText()
        {
            string catalogDir = Path.GetDirectoryName(CatalogCachePath);
            Directory.CreateDirectory(catalogDir);

            string tempCatalog = CatalogCachePath + ".download";
            string tempSha = CatalogCacheShaPath + ".download";

            TryDeleteFile(tempCatalog);
            TryDeleteFile(tempSha);

            try
            {
                string snapshotCatalogUrl = CatalogUrl;
                string sourceStatus = "Online catalog verified";

                if (String.Equals(CatalogUrl, AppInfo.CatalogUrl, StringComparison.OrdinalIgnoreCase))
                {
                    string commitSha = ResolveCatalogCommitSha();
                    snapshotCatalogUrl = BuildCatalogUrlForCommit(commitSha);
                    sourceStatus = "Online catalog verified @ " + commitSha.Substring(0, Math.Min(7, commitSha.Length));
                }

                DownloadFile(snapshotCatalogUrl, tempCatalog);
                DownloadFile(snapshotCatalogUrl + ".sha256", tempSha);

                string expected = ParseSha256File(tempSha);
                string actual = Sha256File(tempCatalog);

                if (!String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Catalog SHA-256 verification failed.");

                ReplaceFile(tempCatalog, CatalogCachePath);
                ReplaceFile(tempSha, CatalogCacheShaPath);

                CatalogLoadedFromCache = false;
                CatalogStatus = sourceStatus;
                return File.ReadAllText(CatalogCachePath, Encoding.UTF8);
            }
            catch (Exception onlineError)
            {
                TryDeleteFile(tempCatalog);
                TryDeleteFile(tempSha);

                if (File.Exists(CatalogCachePath) && File.Exists(CatalogCacheShaPath))
                {
                    string expected = ParseSha256File(CatalogCacheShaPath);
                    string actual = Sha256File(CatalogCachePath);

                    if (String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        CatalogLoadedFromCache = true;
                        CatalogStatus = "Verified cached catalog (offline fallback)";
                        return File.ReadAllText(CatalogCachePath, Encoding.UTF8);
                    }
                }

                throw new InvalidOperationException(
                    "Unable to load the public catalog and no verified cached catalog is available. " +
                    onlineError.Message,
                    onlineError);
            }
        }

        private static string ResolveCatalogCommitSha()
        {
            string apiUrl =
                "https://api.github.com/repos/" + AppInfo.GitHubOwner + "/" + AppInfo.CatalogRepository +
                "/git/ref/heads/" + Uri.EscapeDataString(AppInfo.CatalogBranch);

            string json = DownloadText(apiUrl, true);
            GitHubRefResponse response = JsonUtil.Deserialize<GitHubRefResponse>(json);

            if (response == null || response.@object == null || String.IsNullOrWhiteSpace(response.@object.sha))
                throw new InvalidOperationException("GitHub did not return a catalog commit SHA.");

            string sha = response.@object.sha.Trim();
            if ((sha.Length != 40 && sha.Length != 64) || !sha.All(IsHexCharacter))
                throw new InvalidOperationException("GitHub returned an invalid catalog commit SHA.");

            return sha.ToLowerInvariant();
        }

        private static string BuildCatalogUrlForCommit(string commitSha)
        {
            return
                "https://raw.githubusercontent.com/" + AppInfo.GitHubOwner + "/" + AppInfo.CatalogRepository + "/" +
                commitSha + "/" + AppInfo.CatalogRelativePath;
        }

        private static string DownloadText(string url, bool githubApi)
        {
            ServicePointManager.SecurityProtocol =
                ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072;

            using (WebClient client = new WebClient())
            {
                client.Headers["User-Agent"] =
                    "StrandedDeepModManager/" + AppInfo.Version;

                if (githubApi)
                    client.Headers["Accept"] = "application/vnd.github+json";

                return client.DownloadString(url);
            }
        }

        private static string ParseSha256File(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8).Trim();
            string[] parts = text.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 || parts[0].Length != 64 || !parts[0].All(IsHexCharacter))
                throw new InvalidOperationException("Invalid SHA-256 checksum file.");

            return parts[0].ToLowerInvariant();
        }

        private static bool IsHexCharacter(char c)
        {
            return (c >= '0' && c <= '9') ||
                   (c >= 'a' && c <= 'f') ||
                   (c >= 'A' && c <= 'F');
        }

        private static void DownloadFileVerified(string url, string destination, string expectedSha)
        {
            string temp = destination + ".part";
            TryDeleteFile(temp);

            try
            {
                DownloadFile(url, temp);

                string actual = Sha256File(temp);
                if (!String.Equals(actual, expectedSha, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Downloaded ZIP SHA-256 mismatch.");

                ReplaceFile(temp, destination);
            }
            catch
            {
                TryDeleteFile(temp);
                throw;
            }
        }

        private static void DownloadFile(string url, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));

            ServicePointManager.SecurityProtocol =
                ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072;

            using (WebClient client = new WebClient())
            {
                client.Headers["User-Agent"] =
                    "StrandedDeepModManager/" + AppInfo.Version;
                client.DownloadFile(url, destination);
            }
        }

        private static void ReplaceFile(string source, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));

            if (File.Exists(destination))
                File.Delete(destination);

            File.Move(source, destination);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static void ValidateRepositoryName(string repository)
        {
            if (String.IsNullOrWhiteSpace(repository))
                throw new InvalidOperationException("Repository name is missing.");

            foreach (char c in repository)
            {
                if (!(Char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.'))
                    throw new InvalidOperationException("Unsafe repository name: " + repository);
            }
        }

        private static void ValidateFileName(string fileName)
        {
            if (String.IsNullOrWhiteSpace(fileName) ||
                fileName.IndexOf('/') >= 0 ||
                fileName.IndexOf('\\') >= 0 ||
                fileName.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("Unsafe release asset name: " + fileName);
            }
        }

        private static void ValidateOwnedPath(string relative)
        {
            string safe = NormalizeRelative(relative);

            if (!safe.StartsWith("BepInEx/plugins/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ownedPath must be inside BepInEx/plugins: " + relative);

            if (String.Equals(safe, "BepInEx/plugins", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unsafe ownedPath refused: " + relative);
        }

        private static void VerifyPayloadOwnership(FilesManifestRoot files, IList<string> ownedPaths)
        {
            List<string> normalizedOwned = new List<string>();
            foreach (string ownedPath in ownedPaths)
                normalizedOwned.Add(NormalizeRelative(ownedPath).TrimEnd('/'));

            foreach (PackageFileEntry entry in files.files)
            {
                string relative = NormalizeRelative(entry.path);
                if (!relative.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool covered = false;
                foreach (string owned in normalizedOwned)
                {
                    if (String.Equals(relative, owned, StringComparison.OrdinalIgnoreCase) ||
                        relative.StartsWith(owned + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        covered = true;
                        break;
                    }
                }

                if (!covered)
                    throw new InvalidOperationException("Package payload is outside ownedPaths: " + relative);
            }
        }

        private string StateDirectory
        {
            get { return Path.Combine(GameRoot, "BepInEx", "ModManager", "installed"); }
        }

        private string GetStatePath(string id)
        {
            return Path.Combine(StateDirectory, SafeFileName(id) + ".json");
        }

        private InstalledState ReadInstalledState(string id)
        {
            string path = GetStatePath(id);
            if (!File.Exists(path))
                return null;

            try
            {
                return JsonUtil.ReadFile<InstalledState>(path);
            }
            catch
            {
                return null;
            }
        }

        private void WriteInstalledState(InstalledState state)
        {
            Directory.CreateDirectory(StateDirectory);
            JsonUtil.WriteFile(GetStatePath(state.id), state);
        }

        private void DeleteInstalledState(string id)
        {
            string path = GetStatePath(id);
            if (File.Exists(path))
                File.Delete(path);
        }

        private string ExtractPackageSafely(string zipPath)
        {
            string temp = Path.Combine(
                Path.GetTempPath(),
                "sdmm-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(temp);

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (String.IsNullOrEmpty(entry.FullName))
                            continue;

                        string normalized = NormalizeRelative(entry.FullName);

                        if (String.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(SafeCombine(temp, normalized));
                            continue;
                        }

                        string destination = SafeCombine(temp, normalized);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));

                        using (Stream input = entry.Open())
                        using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            input.CopyTo(output);
                        }
                    }
                }

                return temp;
            }
            catch
            {
                TryDeleteDirectory(temp);
                throw;
            }
        }

        private static string NormalizeRelative(string relative)
        {
            ValidateRelativePath(relative);
            return relative.Replace('\\', '/').TrimStart('/');
        }

        private static void ValidateRelativePath(string relative)
        {
            if (String.IsNullOrWhiteSpace(relative))
                throw new InvalidOperationException("Empty relative path.");

            string normalized = relative.Replace('/', '\\');

            if (Path.IsPathRooted(normalized))
                throw new InvalidOperationException("Absolute path refused: " + relative);

            string[] parts = normalized.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                if (part == "..")
                    throw new InvalidOperationException("Parent traversal refused: " + relative);
            }
        }

        private static string SafeCombine(string root, string relative)
        {
            ValidateRelativePath(relative);

            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path escaped root: " + relative);

            return full;
        }

        private static string GetRelativePath(string root, string fullPath)
        {
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fileFull = Path.GetFullPath(fullPath);

            if (!fileFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path is outside root.");

            return fileFull.Substring(rootFull.Length);
        }

        private static string Sha256File(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder sb = new StringBuilder(hash.Length * 2);

                foreach (byte b in hash)
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));

                return sb.ToString();
            }
        }

        private static void CopyDirectoryLongPathSafe(string source, string destination)
        {
            if (!Directory.Exists(source))
                return;

            Directory.CreateDirectory(destination);

            string systemDirectory =
                Environment.GetFolderPath(Environment.SpecialFolder.System);

            string robocopy = Path.Combine(systemDirectory, "robocopy.exe");

            if (!File.Exists(robocopy))
                throw new InvalidOperationException("robocopy.exe is required for persistent-data backup.");

            if (source.IndexOf('"') >= 0 || destination.IndexOf('"') >= 0)
                throw new InvalidOperationException("Unsafe quote character in backup path.");

            System.Diagnostics.ProcessStartInfo start =
                new System.Diagnostics.ProcessStartInfo();

            start.FileName = robocopy;
            start.Arguments =
                "\"" + source + "\" \"" + destination + "\"" +
                " /E /COPY:DAT /DCOPY:T /R:1 /W:1 /XJ /NFL /NDL /NJH /NJS /NP";
            start.UseShellExecute = false;
            start.CreateNoWindow = true;

            using (System.Diagnostics.Process process =
                System.Diagnostics.Process.Start(start))
            {
                if (process == null)
                    throw new InvalidOperationException("Failed to start robocopy.exe.");

                process.WaitForExit();

                // Robocopy exit codes 0..7 are success / success-with-differences.
                if (process.ExitCode > 7)
                {
                    throw new IOException(
                        "Persistent-data backup failed. robocopy exit code " +
                        process.ExitCode.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = GetRelativePath(source, dir);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = GetRelativePath(source, file);
                string dest = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(file, dest, true);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }

        private static string SafeFileName(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return "package";

            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');

            return value;
        }

        private void EnsureCatalog()
        {
            if (Catalog == null)
                throw new InvalidOperationException("Catalog is not loaded.");
        }

        private static void EnsureGameNotRunning()
        {
            string[] names = new string[]
            {
                "Stranded_Deep",
                "Stranded Deep",
                "StrandedDeep"
            };

            foreach (string name in names)
            {
                try
                {
                    if (Process.GetProcessesByName(name).Length > 0)
                        throw new InvalidOperationException("Close Stranded Deep before modifying mods.");
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch
                {
                }
            }
        }
    }
}
