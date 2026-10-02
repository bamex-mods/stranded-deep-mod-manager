using System;
using System.Collections.Generic;

namespace StrandedDeepModManager
{
    public sealed class CatalogRoot
    {
        public int schemaVersion { get; set; }
        public CatalogGame game { get; set; }
        public List<CatalogPackage> packages { get; set; }
    }

    public sealed class CatalogGame
    {
        public string id { get; set; }
        public string name { get; set; }
        public string platform { get; set; }
    }

    public sealed class CatalogPackage
    {
        public string id { get; set; }
        public string name { get; set; }
        public string category { get; set; }
        public string description { get; set; }
        public string repository { get; set; }
        public CatalogLatest latest { get; set; }
        public CatalogPageRef page { get; set; }
    }

    public sealed class CatalogPageRef
    {
        public string repository { get; set; }
        public string commit { get; set; }
        public string path { get; set; }
    }

    public sealed class CatalogLatest
    {
        public string version { get; set; }
        public string tag { get; set; }
        public string releaseChannel { get; set; }
        public CatalogDownload download { get; set; }
    }

    public sealed class CatalogDownload
    {
        public string provider { get; set; }
        public string asset { get; set; }
        public string sha256 { get; set; }
    }

    public sealed class GitHubRefResponse
    {
        public GitHubRefObject @object { get; set; }
    }

    public sealed class GitHubRefObject
    {
        public string sha { get; set; }
    }

    public sealed class ManifestRoot
    {
        public int schemaVersion { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string type { get; set; }
        public string category { get; set; }
        public string description { get; set; }
        public ManifestGame game { get; set; }
        public ManifestInstall install { get; set; }
        public List<DependencySpec> dependencies { get; set; }
        public List<DependencySpec> optionalDependencies { get; set; }
        public List<object> conflicts { get; set; }
        public PersistenceSpec persistence { get; set; }
        public List<string> ownedPaths { get; set; }
        public string bepInExGuid { get; set; }
    }

    public sealed class ManifestGame
    {
        public string id { get; set; }
        public string platform { get; set; }
    }

    public sealed class ManifestInstall
    {
        public bool restartRequired { get; set; }
    }

    public sealed class DependencySpec
    {
        public string id { get; set; }
        public bool required { get; set; }
        public string minimumVersion { get; set; }
    }

    public sealed class PersistenceSpec
    {
        public bool usesBepInExConfig { get; set; }
        public bool usesGameSave { get; set; }
        public bool usesSidecarData { get; set; }
        public bool backupBeforeUpdate { get; set; }
    }

    public sealed class FilesManifestRoot
    {
        public int schemaVersion { get; set; }
        public string packageId { get; set; }
        public string version { get; set; }
        public List<PackageFileEntry> files { get; set; }
    }

    public sealed class PackageFileEntry
    {
        public string path { get; set; }
        public string sha256 { get; set; }
        public long size { get; set; }
    }

    public sealed class InstalledState
    {
        public int schemaVersion { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string packageSha256 { get; set; }
        public string installedAt { get; set; }
        public List<string> ownedPaths { get; set; }
        public List<PackageFileEntry> files { get; set; }
        public PersistenceSpec persistence { get; set; }
    }

    public sealed class PackageInspection
    {
        public string ZipPath { get; set; }
        public ManifestRoot Manifest { get; set; }
        public FilesManifestRoot Files { get; set; }
    }

    public enum PackageStatusKind
    {
        NotInstalled,
        Installed,
        UpdateAvailable,
        DifferentBuild,
        Modified,
        PackageMissing,
        Error
    }

    public sealed class PackageStatus
    {
        public CatalogPackage CatalogPackage { get; set; }
        public PackageStatusKind Kind { get; set; }
        public string InstalledVersion { get; set; }
        public string Detail { get; set; }
        public string PackageZipPath { get; set; }
        public ManifestRoot LatestManifest { get; set; }
    }

    public sealed class ManagerSettings
    {
        public string gameRoot { get; set; }
    }
}
