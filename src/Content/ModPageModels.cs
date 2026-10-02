using System.Collections.Generic;

namespace StrandedDeepModManager.Content
{
    public sealed class ModPageRoot
    {
        public int schemaVersion { get; set; }
        public string id { get; set; }
        public string defaultLocale { get; set; }
        public Dictionary<string, string> locales { get; set; }
        public ModPageMedia media { get; set; }
        public List<string> tags { get; set; }
        public List<string> highlights { get; set; }
    }

    public sealed class ModPageMedia
    {
        public string icon { get; set; }
        public string cover { get; set; }
        public List<ModPageScreenshot> screenshots { get; set; }
    }

    public sealed class ModPageScreenshot
    {
        public string id { get; set; }
        public string file { get; set; }
    }

    public sealed class ModPageLocaleRoot
    {
        public int schemaVersion { get; set; }
        public string locale { get; set; }
        public string subtitle { get; set; }
        public string description { get; set; }
        public List<string> features { get; set; }
        public Dictionary<string, string> screenshotCaptions { get; set; }
        public List<ModPageFaqItem> faq { get; set; }
        public List<ModPageChangelogEntry> changelog { get; set; }
    }

    public sealed class ModPageFaqItem
    {
        public string question { get; set; }
        public string answer { get; set; }
    }

    public sealed class ModPageChangelogEntry
    {
        public string version { get; set; }
        public string date { get; set; }
        public List<string> items { get; set; }
    }

    public sealed class PageCachePointer
    {
        public int schemaVersion { get; set; }
        public string id { get; set; }
        public string repository { get; set; }
        public string commit { get; set; }
        public string path { get; set; }
    }

    public enum ModPageSourceKind
    {
        Online,
        ExactCache,
        LastGoodCache
    }

    public sealed class ModPageLoadResult
    {
        public string PackageId { get; set; }
        public string Repository { get; set; }
        public string RequestedCommit { get; set; }
        public string ActualCommit { get; set; }
        public string PagePath { get; set; }
        public string RequestedLocale { get; set; }
        public string ResolvedLocale { get; set; }
        public ModPageRoot Page { get; set; }
        public ModPageLocaleRoot Locale { get; set; }
        public string CoverPath { get; set; }
        public ModPageSourceKind SourceKind { get; set; }
        public string Warning { get; set; }
        public string MediaWarning { get; set; }
    }
}
