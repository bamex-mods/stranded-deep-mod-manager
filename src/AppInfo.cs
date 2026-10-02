namespace StrandedDeepModManager
{
    internal static class AppInfo
    {
        public const string Version = "0.2.0";
        public const string ProductName = "Stranded Deep Mod Manager";
        public const string GitHubOwner = "bamex-mods";
        public const string CatalogRepository = "stranded-deep-mod-catalog";
        public const string CatalogBranch = "main";
        public const string CatalogRelativePath = "stable/catalog.json";
        public const string CatalogUrl =
            "https://raw.githubusercontent.com/" + GitHubOwner + "/" + CatalogRepository + "/" + CatalogBranch + "/" + CatalogRelativePath;
    }
}
