using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace StrandedDeepModManager.Content
{
    public sealed class ModPageService
    {
        public const int PageJsonMaxBytes = 256 * 1024;
        public const int LocaleJsonMaxBytes = 512 * 1024;
        public const int IconMaxBytes = 1 * 1024 * 1024;
        public const int CoverMaxBytes = 2 * 1024 * 1024;
        public const int ScreenshotMaxBytes = 4 * 1024 * 1024;
        public const long SnapshotMaxBytes = 40L * 1024L * 1024L;
        public const int MaxScreenshots = 12;
        public const int MaxImageDimension = 8192;
        public const long MaxImagePixels = 32L * 1024L * 1024L;

        private static readonly Regex PackageIdPattern =
            new Regex("^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant);

        private static readonly Regex RepositoryPattern =
            new Regex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant);

        private static readonly Regex CommitPattern =
            new Regex("^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant);

        private static readonly Regex LocalePattern =
            new Regex("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant);

        private static readonly Regex SemanticKeyPattern =
            new Regex("^[a-z0-9][a-z0-9_]*$", RegexOptions.CultureInvariant);

        private static readonly Regex ScreenshotIdPattern =
            new Regex("^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant);

        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);

        private readonly PageCache _cache;

        public bool NetworkEnabled { get; set; }

        public ModPageService(PageCache cache)
        {
            if (cache == null)
                throw new ArgumentNullException("cache");

            _cache = cache;
            NetworkEnabled = true;
        }

        public ModPageLoadResult Load(
            CatalogPackage package,
            string requestedLocale)
        {
            if (package == null)
                throw new ArgumentNullException("package");

            ValidatePackageId(package.id);

            if (package.page == null)
                return null;

            PageCachePointer requestedPointer =
                CreateValidatedPointer(package);

            string onlineFailure = null;

            if (NetworkEnabled)
            {
                try
                {
                    return LoadOnline(
                        package,
                        requestedPointer,
                        requestedLocale);
                }
                catch (Exception ex)
                {
                    onlineFailure =
                        "Online page load failed: " +
                        SafeMessage(ex);
                }
            }
            else
            {
                onlineFailure = "Network disabled.";
            }

            string exactFailure = null;

            try
            {
                return LoadFromCache(
                    package.id,
                    requestedPointer,
                    requestedLocale,
                    ModPageSourceKind.ExactCache,
                    onlineFailure);
            }
            catch (Exception ex)
            {
                exactFailure =
                    "Exact cache unavailable: " +
                    SafeMessage(ex);
            }

            PageCachePointer lastGood;

            if (_cache.TryReadLastGood(package.id, out lastGood))
            {
                ValidatePointer(lastGood, package.id);

                bool sameSnapshot =
                    String.Equals(
                        lastGood.commit,
                        requestedPointer.commit,
                        StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(
                        lastGood.repository,
                        requestedPointer.repository,
                        StringComparison.Ordinal) &&
                    String.Equals(
                        NormalizeRepoPath(lastGood.path),
                        NormalizeRepoPath(requestedPointer.path),
                        StringComparison.Ordinal);

                if (!sameSnapshot)
                {
                    try
                    {
                        return LoadFromCache(
                            package.id,
                            lastGood,
                            requestedLocale,
                            ModPageSourceKind.LastGoodCache,
                            CombineMessages(
                                onlineFailure,
                                exactFailure));
                    }
                    catch (Exception ex)
                    {
                        exactFailure = CombineMessages(
                            exactFailure,
                            "Last-good cache unavailable: " +
                            SafeMessage(ex));
                    }
                }
            }

            throw new InvalidOperationException(
                "Mod page is unavailable. " +
                CombineMessages(
                    onlineFailure,
                    exactFailure));
        }

        public string EnsureScreenshot(
            CatalogPackage package,
            ModPageLoadResult pageResult,
            string screenshotId)
        {
            if (package == null)
                throw new ArgumentNullException("package");

            if (pageResult == null ||
                pageResult.Page == null)
            {
                throw new InvalidOperationException(
                    "A loaded mod page is required.");
            }

            ValidatePackageId(package.id);

            if (!String.Equals(
                package.id,
                pageResult.PackageId,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Loaded page does not belong to this package.");
            }

            if (String.IsNullOrWhiteSpace(screenshotId))
                throw new InvalidOperationException("Screenshot id is required.");

            ModPageScreenshot screenshot =
                pageResult.Page.media.screenshots.FirstOrDefault(
                    x => x != null &&
                         String.Equals(
                             x.id,
                             screenshotId,
                             StringComparison.Ordinal));

            if (screenshot == null)
                throw new InvalidOperationException(
                    "Screenshot not found: " + screenshotId);

            ValidateImageRelativePath(
                screenshot.file,
                "screenshot");

            string cachedPath = _cache.GetMediaPath(
                pageResult.PackageId,
                pageResult.ActualCommit,
                screenshot.file);

            if (File.Exists(cachedPath))
            {
                try
                {
                    byte[] cachedBytes =
                        File.ReadAllBytes(cachedPath);

                    ValidateImageBytes(
                        cachedBytes,
                        screenshot.file,
                        ScreenshotMaxBytes);

                    return cachedPath;
                }
                catch
                {
                    TryDeleteFile(cachedPath);
                }
            }

            if (!NetworkEnabled)
            {
                throw new InvalidOperationException(
                    "Screenshot is not cached and network access is disabled.");
            }

            ValidateRepositoryName(pageResult.Repository);
            ValidateCommit(pageResult.ActualCommit);
            string pagePath = ValidateJsonRepoPath(
                pageResult.PagePath,
                "page path");

            string remotePath = ResolvePageChildPath(
                pagePath,
                screenshot.file);

            string url = BuildRawUrl(
                pageResult.Repository,
                pageResult.ActualCommit,
                remotePath);

            byte[] bytes = DownloadBytesBounded(
                url,
                ScreenshotMaxBytes);

            ValidateImageBytes(
                bytes,
                screenshot.file,
                ScreenshotMaxBytes);

            EnsureSnapshotBudget(
                pageResult.PackageId,
                pageResult.ActualCommit,
                screenshot.file,
                bytes.LongLength);

            _cache.StoreMedia(
                pageResult.PackageId,
                pageResult.ActualCommit,
                screenshot.file,
                bytes);

            return _cache.GetMediaPath(
                pageResult.PackageId,
                pageResult.ActualCommit,
                screenshot.file);
        }

        private ModPageLoadResult LoadOnline(
            CatalogPackage package,
            PageCachePointer pointer,
            string requestedLocale)
        {
            string pageUrl = BuildRawUrl(
                pointer.repository,
                pointer.commit,
                pointer.path);

            byte[] pageBytes = DownloadBytesBounded(
                pageUrl,
                PageJsonMaxBytes);

            ModPageRoot page = ParseAndValidatePage(
                pageBytes,
                package.id);

            string resolvedLocale;
            byte[] localeBytes;
            ModPageLocaleRoot locale;

            LoadLocaleOnline(
                pointer,
                page,
                requestedLocale,
                out resolvedLocale,
                out localeBytes,
                out locale);

            _cache.StoreCore(
                pointer,
                resolvedLocale,
                pageBytes,
                localeBytes);

            string coverPath = null;
            string mediaWarning = null;

            if (page.media != null &&
                !String.IsNullOrWhiteSpace(page.media.cover))
            {
                try
                {
                    coverPath = LoadCoverOnline(
                        pointer,
                        page.media.cover);
                }
                catch (Exception ex)
                {
                    mediaWarning =
                        "Cover unavailable: " +
                        SafeMessage(ex);

                    coverPath = TryGetValidatedCachedImage(
                        pointer.id,
                        pointer.commit,
                        page.media.cover,
                        CoverMaxBytes);
                }
            }

            ModPageLoadResult result =
                new ModPageLoadResult();

            result.PackageId = package.id;
            result.Repository = pointer.repository;
            result.RequestedCommit = pointer.commit;
            result.ActualCommit = pointer.commit;
            result.PagePath = pointer.path;
            result.RequestedLocale = requestedLocale;
            result.ResolvedLocale = resolvedLocale;
            result.Page = page;
            result.Locale = locale;
            result.CoverPath = coverPath;
            result.SourceKind = ModPageSourceKind.Online;
            result.Warning = null;
            result.MediaWarning = mediaWarning;

            return result;
        }

        private ModPageLoadResult LoadFromCache(
            string packageId,
            PageCachePointer pointer,
            string requestedLocale,
            ModPageSourceKind sourceKind,
            string warning)
        {
            ValidatePointer(pointer, packageId);

            byte[] pageBytes;

            if (!_cache.TryReadPageBytes(
                pointer.id,
                pointer.commit,
                out pageBytes))
            {
                throw new InvalidOperationException(
                    "Cached page.json is missing.");
            }

            if (pageBytes.LongLength > PageJsonMaxBytes)
                throw new InvalidOperationException(
                    "Cached page.json exceeds the size limit.");

            ModPageRoot page = ParseAndValidatePage(
                pageBytes,
                packageId);

            string resolvedLocale = null;
            byte[] localeBytes = null;
            ModPageLocaleRoot locale = null;

            List<string> candidates =
                BuildLocaleCandidates(
                    page,
                    requestedLocale);

            List<string> failures =
                new List<string>();

            foreach (string candidate in candidates)
            {
                byte[] candidateBytes;

                if (!_cache.TryReadLocaleBytes(
                    pointer.id,
                    pointer.commit,
                    candidate,
                    out candidateBytes))
                {
                    failures.Add(candidate + ": not cached");
                    continue;
                }

                try
                {
                    if (candidateBytes.LongLength > LocaleJsonMaxBytes)
                        throw new InvalidOperationException(
                            "cached locale exceeds the size limit");

                    ModPageLocaleRoot parsed =
                        ParseAndValidateLocale(
                            candidateBytes,
                            candidate);

                    resolvedLocale = candidate;
                    localeBytes = candidateBytes;
                    locale = parsed;
                    break;
                }
                catch (Exception ex)
                {
                    failures.Add(
                        candidate +
                        ": " +
                        SafeMessage(ex));
                }
            }

            if (locale == null)
            {
                throw new InvalidOperationException(
                    "No valid cached locale is available. " +
                    String.Join("; ", failures.ToArray()));
            }

            string coverPath = null;
            string mediaWarning = null;

            if (!String.IsNullOrWhiteSpace(page.media.cover))
            {
                coverPath = TryGetValidatedCachedImage(
                    pointer.id,
                    pointer.commit,
                    page.media.cover,
                    CoverMaxBytes);

                if (coverPath == null)
                    mediaWarning = "Cover is not available in validated cache.";
            }

            ModPageLoadResult result =
                new ModPageLoadResult();

            result.PackageId = packageId;
            result.Repository = pointer.repository;
            result.RequestedCommit = pointer.commit;
            result.ActualCommit = pointer.commit;
            result.PagePath = pointer.path;
            result.RequestedLocale = requestedLocale;
            result.ResolvedLocale = resolvedLocale;
            result.Page = page;
            result.Locale = locale;
            result.CoverPath = coverPath;
            result.SourceKind = sourceKind;
            result.Warning = warning;
            result.MediaWarning = mediaWarning;

            return result;
        }

        private void LoadLocaleOnline(
            PageCachePointer pointer,
            ModPageRoot page,
            string requestedLocale,
            out string resolvedLocale,
            out byte[] localeBytes,
            out ModPageLocaleRoot locale)
        {
            resolvedLocale = null;
            localeBytes = null;
            locale = null;

            List<string> candidates =
                BuildLocaleCandidates(
                    page,
                    requestedLocale);

            List<string> failures =
                new List<string>();

            foreach (string candidate in candidates)
            {
                try
                {
                    string localeRelative =
                        page.locales[candidate];

                    string remotePath =
                        ResolvePageChildPath(
                            pointer.path,
                            localeRelative);

                    string url = BuildRawUrl(
                        pointer.repository,
                        pointer.commit,
                        remotePath);

                    byte[] downloaded =
                        DownloadBytesBounded(
                            url,
                            LocaleJsonMaxBytes);

                    ModPageLocaleRoot parsed =
                        ParseAndValidateLocale(
                            downloaded,
                            candidate);

                    resolvedLocale = candidate;
                    localeBytes = downloaded;
                    locale = parsed;
                    return;
                }
                catch (Exception ex)
                {
                    failures.Add(
                        candidate +
                        ": " +
                        SafeMessage(ex));
                }
            }

            throw new InvalidOperationException(
                "No valid locale could be loaded. " +
                String.Join("; ", failures.ToArray()));
        }

        private string LoadCoverOnline(
            PageCachePointer pointer,
            string coverRelative)
        {
            ValidateImageRelativePath(
                coverRelative,
                "cover");

            string remotePath = ResolvePageChildPath(
                pointer.path,
                coverRelative);

            string url = BuildRawUrl(
                pointer.repository,
                pointer.commit,
                remotePath);

            byte[] bytes = DownloadBytesBounded(
                url,
                CoverMaxBytes);

            ValidateImageBytes(
                bytes,
                coverRelative,
                CoverMaxBytes);

            EnsureSnapshotBudget(
                pointer.id,
                pointer.commit,
                coverRelative,
                bytes.LongLength);

            _cache.StoreMedia(
                pointer.id,
                pointer.commit,
                coverRelative,
                bytes);

            return _cache.GetMediaPath(
                pointer.id,
                pointer.commit,
                coverRelative);
        }

        private string TryGetValidatedCachedImage(
            string packageId,
            string commit,
            string relativePath,
            int maxBytes)
        {
            try
            {
                string path = _cache.GetMediaPath(
                    packageId,
                    commit,
                    relativePath);

                if (!File.Exists(path))
                    return null;

                byte[] bytes = File.ReadAllBytes(path);

                ValidateImageBytes(
                    bytes,
                    relativePath,
                    maxBytes);

                return path;
            }
            catch
            {
                return null;
            }
        }

        private PageCachePointer CreateValidatedPointer(
            CatalogPackage package)
        {
            if (package.page == null)
                throw new InvalidOperationException(
                    "Package has no mod-page reference.");

            PageCachePointer pointer =
                new PageCachePointer();

            pointer.schemaVersion = 1;
            pointer.id = package.id;
            pointer.repository =
                package.page.repository == null
                    ? null
                    : package.page.repository.Trim();
            pointer.commit =
                package.page.commit == null
                    ? null
                    : package.page.commit.Trim().ToLowerInvariant();
            pointer.path =
                package.page.path == null
                    ? null
                    : NormalizeRepoPath(package.page.path);

            ValidatePointer(pointer, package.id);

            return pointer;
        }

        private static void ValidatePointer(
            PageCachePointer pointer,
            string expectedPackageId)
        {
            if (pointer == null)
                throw new InvalidOperationException(
                    "Mod-page pointer is missing.");

            if (pointer.schemaVersion != 1)
                throw new InvalidOperationException(
                    "Unsupported mod-page pointer schema.");

            ValidatePackageId(pointer.id);

            if (!String.Equals(
                pointer.id,
                expectedPackageId,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Mod-page pointer package id mismatch.");
            }

            ValidateRepositoryName(pointer.repository);
            ValidateCommit(pointer.commit);
            pointer.path = ValidateJsonRepoPath(
                pointer.path,
                "page path");
        }

        private static ModPageRoot ParseAndValidatePage(
            byte[] bytes,
            string expectedPackageId)
        {
            string text = DecodeUtf8Json(
                bytes,
                PageJsonMaxBytes,
                "page.json");

            ValidatePageJsonShape(text);

            ModPageRoot page =
                JsonUtil.Deserialize<ModPageRoot>(text);

            if (page == null || page.schemaVersion != 1)
                throw new InvalidOperationException(
                    "Unsupported or invalid mod-page schema.");

            ValidatePackageId(page.id);

            if (!String.Equals(
                page.id,
                expectedPackageId,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Remote mod-page id does not match catalog package id.");
            }

            if (page.locales == null ||
                page.locales.Count == 0 ||
                page.locales.Count > 16)
            {
                throw new InvalidOperationException(
                    "Mod-page locale map is invalid.");
            }

            if (String.IsNullOrWhiteSpace(page.defaultLocale))
                throw new InvalidOperationException(
                    "Mod-page defaultLocale is missing.");

            string defaultLocaleKey =
                FindLocaleKey(
                    page.locales,
                    page.defaultLocale);

            if (defaultLocaleKey == null)
                throw new InvalidOperationException(
                    "Mod-page defaultLocale is not present in locales.");

            page.defaultLocale = defaultLocaleKey;

            foreach (KeyValuePair<string, string> item in page.locales)
            {
                ValidateLocaleKey(item.Key);

                item.Value.Equals(item.Value);
                ValidateJsonRepoPath(
                    item.Value,
                    "locale file");
            }

            if (page.media == null)
                throw new InvalidOperationException(
                    "Mod-page media object is missing.");

            if (!String.IsNullOrWhiteSpace(page.media.icon))
            {
                ValidateImageRelativePath(
                    page.media.icon,
                    "icon");
            }

            if (!String.IsNullOrWhiteSpace(page.media.cover))
            {
                ValidateImageRelativePath(
                    page.media.cover,
                    "cover");
            }

            if (page.media.screenshots == null)
                page.media.screenshots =
                    new List<ModPageScreenshot>();

            if (page.media.screenshots.Count > MaxScreenshots)
                throw new InvalidOperationException(
                    "Mod-page has too many screenshots.");

            HashSet<string> screenshotIds =
                new HashSet<string>(
                    StringComparer.Ordinal);

            foreach (ModPageScreenshot screenshot
                in page.media.screenshots)
            {
                if (screenshot == null ||
                    String.IsNullOrWhiteSpace(screenshot.id) ||
                    !ScreenshotIdPattern.IsMatch(screenshot.id))
                {
                    throw new InvalidOperationException(
                        "Invalid screenshot id.");
                }

                if (!screenshotIds.Add(screenshot.id))
                    throw new InvalidOperationException(
                        "Duplicate screenshot id: " +
                        screenshot.id);

                ValidateImageRelativePath(
                    screenshot.file,
                    "screenshot");
            }

            if (page.tags == null)
                throw new InvalidOperationException(
                    "Mod-page tags are missing.");

            if (page.tags.Count > 64)
                throw new InvalidOperationException(
                    "Mod-page has too many tags.");

            ValidateSemanticKeys(
                page.tags,
                "tag");

            if (page.highlights == null)
                throw new InvalidOperationException(
                    "Mod-page highlights are missing.");

            if (page.highlights.Count > 4)
                throw new InvalidOperationException(
                    "Mod-page has more than four highlights.");

            ValidateSemanticKeys(
                page.highlights,
                "highlight");

            return page;
        }

        private static ModPageLocaleRoot ParseAndValidateLocale(
            byte[] bytes,
            string expectedLocale)
        {
            string text = DecodeUtf8Json(
                bytes,
                LocaleJsonMaxBytes,
                "locale JSON");

            ValidateLocaleJsonShape(text);

            ModPageLocaleRoot locale =
                JsonUtil.Deserialize<ModPageLocaleRoot>(text);

            if (locale == null ||
                locale.schemaVersion != 1)
            {
                throw new InvalidOperationException(
                    "Unsupported or invalid locale schema.");
            }

            ValidateLocaleKey(locale.locale);

            if (!String.Equals(
                locale.locale,
                expectedLocale,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Locale file identity does not match its locale key.");
            }

            if (locale.subtitle == null ||
                locale.subtitle.Length > 2000)
            {
                throw new InvalidOperationException(
                    "Locale subtitle is invalid.");
            }

            if (locale.description == null ||
                locale.description.Length > 50000)
            {
                throw new InvalidOperationException(
                    "Locale description is invalid.");
            }

            if (locale.features == null)
                locale.features = new List<string>();

            if (locale.features.Count > 128)
                throw new InvalidOperationException(
                    "Locale has too many features.");

            foreach (string feature in locale.features)
            {
                if (feature == null || feature.Length > 8000)
                    throw new InvalidOperationException(
                        "Locale feature text is invalid.");
            }

            if (locale.screenshotCaptions == null)
            {
                locale.screenshotCaptions =
                    new Dictionary<string, string>();
            }

            if (locale.screenshotCaptions.Count > MaxScreenshots)
                throw new InvalidOperationException(
                    "Locale has too many screenshot captions.");

            foreach (KeyValuePair<string, string> item
                in locale.screenshotCaptions)
            {
                if (!ScreenshotIdPattern.IsMatch(item.Key) ||
                    item.Value == null ||
                    item.Value.Length > 8000)
                {
                    throw new InvalidOperationException(
                        "Invalid screenshot caption.");
                }
            }

            if (locale.faq == null)
                locale.faq = new List<ModPageFaqItem>();

            if (locale.faq.Count > 64)
                throw new InvalidOperationException(
                    "Locale has too many FAQ items.");

            foreach (ModPageFaqItem faq in locale.faq)
            {
                if (faq == null ||
                    faq.question == null ||
                    faq.answer == null ||
                    faq.question.Length > 4000 ||
                    faq.answer.Length > 20000)
                {
                    throw new InvalidOperationException(
                        "Invalid FAQ item.");
                }
            }

            if (locale.changelog == null)
            {
                locale.changelog =
                    new List<ModPageChangelogEntry>();
            }

            if (locale.changelog.Count > 128)
                throw new InvalidOperationException(
                    "Locale changelog is too large.");

            foreach (ModPageChangelogEntry entry
                in locale.changelog)
            {
                if (entry == null ||
                    String.IsNullOrWhiteSpace(entry.version) ||
                    entry.version.Length > 128)
                {
                    throw new InvalidOperationException(
                        "Invalid changelog entry.");
                }

                if (entry.date != null &&
                    entry.date.Length > 128)
                {
                    throw new InvalidOperationException(
                        "Invalid changelog date.");
                }

                if (entry.items == null ||
                    entry.items.Count > 128)
                {
                    throw new InvalidOperationException(
                        "Invalid changelog items.");
                }

                foreach (string item in entry.items)
                {
                    if (item == null || item.Length > 8000)
                        throw new InvalidOperationException(
                            "Invalid changelog text.");
                }
            }

            return locale;
        }

        private static void ValidatePageJsonShape(string text)
        {
            Dictionary<string, object> root =
                ParseObject(text, "page.json");

            ValidateAllowedKeys(
                root,
                new string[] {
                    "schemaVersion",
                    "id",
                    "defaultLocale",
                    "locales",
                    "media",
                    "tags",
                    "highlights"
                },
                new string[] {
                    "schemaVersion",
                    "id",
                    "defaultLocale",
                    "locales",
                    "media",
                    "tags",
                    "highlights"
                },
                "page.json");

            Dictionary<string, object> locales =
                RequireObject(
                    root["locales"],
                    "page.locales");

            foreach (KeyValuePair<string, object> item
                in locales)
            {
                if (!(item.Value is string))
                    throw new InvalidOperationException(
                        "page.locales values must be strings.");
            }

            Dictionary<string, object> media =
                RequireObject(
                    root["media"],
                    "page.media");

            ValidateAllowedKeys(
                media,
                new string[0],
                new string[] {
                    "icon",
                    "cover",
                    "screenshots"
                },
                "page.media");

            ValidateOptionalString(
                media,
                "icon",
                "page.media.icon");

            ValidateOptionalString(
                media,
                "cover",
                "page.media.cover");

            if (media.ContainsKey("screenshots"))
            {
                object[] screenshots =
                    RequireArray(
                        media["screenshots"],
                        "page.media.screenshots");

                foreach (object item in screenshots)
                {
                    Dictionary<string, object> shot =
                        RequireObject(
                            item,
                            "page.media.screenshot");

                    ValidateAllowedKeys(
                        shot,
                        new string[] {
                            "id",
                            "file"
                        },
                        new string[] {
                            "id",
                            "file"
                        },
                        "page.media.screenshot");

                    RequireString(
                        shot["id"],
                        "screenshot.id");

                    RequireString(
                        shot["file"],
                        "screenshot.file");
                }
            }

            RequireArray(root["tags"], "page.tags");
            RequireArray(root["highlights"], "page.highlights");
        }

        private static void ValidateLocaleJsonShape(string text)
        {
            Dictionary<string, object> root =
                ParseObject(text, "locale JSON");

            ValidateAllowedKeys(
                root,
                new string[] {
                    "schemaVersion",
                    "locale",
                    "subtitle",
                    "description"
                },
                new string[] {
                    "schemaVersion",
                    "locale",
                    "subtitle",
                    "description",
                    "features",
                    "screenshotCaptions",
                    "faq",
                    "changelog"
                },
                "locale JSON");

            RequireString(
                root["locale"],
                "locale.locale");

            RequireString(
                root["subtitle"],
                "locale.subtitle");

            RequireString(
                root["description"],
                "locale.description");

            if (root.ContainsKey("features"))
            {
                object[] features =
                    RequireArray(
                        root["features"],
                        "locale.features");

                foreach (object feature in features)
                    RequireString(
                        feature,
                        "locale.feature");
            }

            if (root.ContainsKey("screenshotCaptions"))
            {
                Dictionary<string, object> captions =
                    RequireObject(
                        root["screenshotCaptions"],
                        "locale.screenshotCaptions");

                foreach (KeyValuePair<string, object> item
                    in captions)
                {
                    RequireString(
                        item.Value,
                        "screenshot caption");
                }
            }

            if (root.ContainsKey("faq"))
            {
                object[] faq =
                    RequireArray(
                        root["faq"],
                        "locale.faq");

                foreach (object item in faq)
                {
                    Dictionary<string, object> faqItem =
                        RequireObject(
                            item,
                            "locale.faq item");

                    ValidateAllowedKeys(
                        faqItem,
                        new string[] {
                            "question",
                            "answer"
                        },
                        new string[] {
                            "question",
                            "answer"
                        },
                        "locale.faq item");

                    RequireString(
                        faqItem["question"],
                        "faq.question");

                    RequireString(
                        faqItem["answer"],
                        "faq.answer");
                }
            }

            if (root.ContainsKey("changelog"))
            {
                object[] changelog =
                    RequireArray(
                        root["changelog"],
                        "locale.changelog");

                foreach (object item in changelog)
                {
                    Dictionary<string, object> entry =
                        RequireObject(
                            item,
                            "locale.changelog entry");

                    ValidateAllowedKeys(
                        entry,
                        new string[] {
                            "version",
                            "items"
                        },
                        new string[] {
                            "version",
                            "date",
                            "items"
                        },
                        "locale.changelog entry");

                    RequireString(
                        entry["version"],
                        "changelog.version");

                    ValidateOptionalString(
                        entry,
                        "date",
                        "changelog.date");

                    object[] items =
                        RequireArray(
                            entry["items"],
                            "changelog.items");

                    foreach (object changelogText in items)
                    {
                        RequireString(
                            changelogText,
                            "changelog item");
                    }
                }
            }
        }

        private static Dictionary<string, object> ParseObject(
            string text,
            string label)
        {
            JavaScriptSerializer serializer =
                new JavaScriptSerializer();

            serializer.MaxJsonLength =
                Math.Max(
                    PageJsonMaxBytes,
                    LocaleJsonMaxBytes);

            serializer.RecursionLimit = 100;

            object value =
                serializer.DeserializeObject(text);

            return RequireObject(value, label);
        }

        private static Dictionary<string, object> RequireObject(
            object value,
            string label)
        {
            Dictionary<string, object> result =
                value as Dictionary<string, object>;

            if (result == null)
                throw new InvalidOperationException(
                    label + " must be a JSON object.");

            return result;
        }

        private static object[] RequireArray(
            object value,
            string label)
        {
            object[] array = value as object[];

            if (array != null)
                return array;

            ArrayList arrayList = value as ArrayList;

            if (arrayList != null)
                return arrayList.ToArray();

            throw new InvalidOperationException(
                label + " must be a JSON array.");
        }

        private static string RequireString(
            object value,
            string label)
        {
            string text = value as string;

            if (text == null)
                throw new InvalidOperationException(
                    label + " must be a string.");

            return text;
        }

        private static void ValidateOptionalString(
            Dictionary<string, object> obj,
            string key,
            string label)
        {
            if (!obj.ContainsKey(key))
                return;

            RequireString(obj[key], label);
        }

        private static void ValidateAllowedKeys(
            Dictionary<string, object> obj,
            string[] required,
            string[] allowed,
            string label)
        {
            HashSet<string> allowedSet =
                new HashSet<string>(
                    allowed,
                    StringComparer.Ordinal);

            foreach (string key in obj.Keys)
            {
                if (!allowedSet.Contains(key))
                {
                    throw new InvalidOperationException(
                        "Unsupported property in " +
                        label +
                        ": " +
                        key);
                }
            }

            foreach (string key in required)
            {
                if (!obj.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        "Required property missing in " +
                        label +
                        ": " +
                        key);
                }
            }
        }

        private static List<string> BuildLocaleCandidates(
            ModPageRoot page,
            string requestedLocale)
        {
            List<string> result =
                new List<string>();

            if (!String.IsNullOrWhiteSpace(requestedLocale))
            {
                string requestedKey =
                    FindLocaleKey(
                        page.locales,
                        requestedLocale.Trim());

                if (requestedKey != null)
                    result.Add(requestedKey);
            }

            string defaultKey =
                FindLocaleKey(
                    page.locales,
                    page.defaultLocale);

            if (defaultKey != null &&
                !result.Contains(
                    defaultKey,
                    StringComparer.OrdinalIgnoreCase))
            {
                result.Add(defaultKey);
            }

            if (result.Count == 0)
            {
                throw new InvalidOperationException(
                    "No usable locale is declared by the mod page.");
            }

            return result;
        }

        private static string FindLocaleKey(
            IDictionary<string, string> locales,
            string requested)
        {
            if (locales == null ||
                String.IsNullOrWhiteSpace(requested))
            {
                return null;
            }

            foreach (string key in locales.Keys)
            {
                if (String.Equals(
                    key,
                    requested,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return key;
                }
            }

            return null;
        }

        private static void ValidateSemanticKeys(
            IList<string> values,
            string label)
        {
            HashSet<string> seen =
                new HashSet<string>(
                    StringComparer.Ordinal);

            foreach (string value in values)
            {
                if (String.IsNullOrWhiteSpace(value) ||
                    !SemanticKeyPattern.IsMatch(value))
                {
                    throw new InvalidOperationException(
                        "Invalid " + label + " key.");
                }

                if (!seen.Add(value))
                {
                    throw new InvalidOperationException(
                        "Duplicate " + label + ": " + value);
                }
            }
        }

        private static void ValidatePackageId(string id)
        {
            if (String.IsNullOrWhiteSpace(id) ||
                !PackageIdPattern.IsMatch(id))
            {
                throw new InvalidOperationException(
                    "Invalid mod package id.");
            }
        }

        private static void ValidateRepositoryName(
            string repository)
        {
            if (String.IsNullOrWhiteSpace(repository) ||
                !RepositoryPattern.IsMatch(repository))
            {
                throw new InvalidOperationException(
                    "Unsafe mod-page repository name.");
            }
        }

        private static void ValidateCommit(string commit)
        {
            if (String.IsNullOrWhiteSpace(commit) ||
                !CommitPattern.IsMatch(commit))
            {
                throw new InvalidOperationException(
                    "Mod-page commit must be an immutable 40-character Git SHA.");
            }
        }

        private static void ValidateLocaleKey(string locale)
        {
            if (String.IsNullOrWhiteSpace(locale) ||
                !LocalePattern.IsMatch(locale))
            {
                throw new InvalidOperationException(
                    "Invalid locale key.");
            }
        }

        private static string ValidateJsonRepoPath(
            string path,
            string label)
        {
            string normalized =
                ValidateRepoPath(path, label);

            if (!normalized.EndsWith(
                ".json",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    label + " must reference a JSON file.");
            }

            return normalized;
        }

        private static void ValidateImageRelativePath(
            string path,
            string label)
        {
            string normalized =
                ValidateRepoPath(path, label);

            string extension =
                Path.GetExtension(normalized).ToLowerInvariant();

            if (extension != ".png" &&
                extension != ".jpg" &&
                extension != ".jpeg")
            {
                throw new InvalidOperationException(
                    label + " uses an unsupported image type.");
            }
        }

        private static string ValidateRepoPath(
            string path,
            string label)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException(
                    label + " is missing.");

            if (path.IndexOf('\\') >= 0 ||
                path.IndexOf(':') >= 0 ||
                path.StartsWith("/", StringComparison.Ordinal) ||
                path.EndsWith("/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Unsafe " + label + ".");
            }

            string normalized = NormalizeRepoPath(path);

            string[] parts =
                normalized.Split('/');

            if (parts.Length == 0)
                throw new InvalidOperationException(
                    "Unsafe " + label + ".");

            foreach (string part in parts)
            {
                if (String.IsNullOrWhiteSpace(part) ||
                    part == "." ||
                    part == ".." ||
                    !RepositoryPattern.IsMatch(part))
                {
                    throw new InvalidOperationException(
                        "Unsafe " + label + ".");
                }
            }

            return normalized;
        }

        private static string NormalizeRepoPath(string path)
        {
            return path == null
                ? null
                : path.Trim().Replace('\\', '/');
        }

        private static string ResolvePageChildPath(
            string pagePath,
            string childPath)
        {
            string safePage =
                ValidateJsonRepoPath(
                    pagePath,
                    "page path");

            string safeChild =
                ValidateRepoPath(
                    childPath,
                    "page child path");

            int slash =
                safePage.LastIndexOf('/');

            if (slash < 0)
                return safeChild;

            return safePage.Substring(
                0,
                slash + 1) +
                safeChild;
        }

        private static string BuildRawUrl(
            string repository,
            string commit,
            string path)
        {
            ValidateRepositoryName(repository);
            ValidateCommit(commit);

            string safePath =
                ValidateRepoPath(
                    path,
                    "remote path");

            string escapedPath =
                String.Join(
                    "/",
                    safePath.Split('/')
                        .Select(
                            part =>
                                Uri.EscapeDataString(part))
                        .ToArray());

            return
                "https://raw.githubusercontent.com/" +
                Uri.EscapeDataString(AppInfo.GitHubOwner) +
                "/" +
                Uri.EscapeDataString(repository) +
                "/" +
                commit.ToLowerInvariant() +
                "/" +
                escapedPath;
        }

        private static byte[] DownloadBytesBounded(
            string url,
            int maxBytes)
        {
            if (maxBytes <= 0)
                throw new ArgumentOutOfRangeException("maxBytes");

            ServicePointManager.SecurityProtocol =
                ServicePointManager.SecurityProtocol |
                (SecurityProtocolType)3072;

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(url);

            request.Method = "GET";
            request.UserAgent =
                "StrandedDeepModManager/" +
                AppInfo.Version;
            request.Accept =
                "application/json,image/png,image/jpeg,*/*";
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 5;
            request.AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate;
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;

            using (HttpWebResponse response =
                (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException(
                        "HTTP " +
                        (int)response.StatusCode +
                        " while loading mod-page content.");
                }

                if (response.ContentLength > maxBytes)
                {
                    throw new InvalidOperationException(
                        "Remote content exceeds the size limit.");
                }

                using (Stream stream =
                    response.GetResponseStream())
                using (MemoryStream memory =
                    new MemoryStream())
                {
                    byte[] buffer =
                        new byte[8192];

                    while (true)
                    {
                        int read =
                            stream.Read(
                                buffer,
                                0,
                                buffer.Length);

                        if (read <= 0)
                            break;

                        if (memory.Length + read > maxBytes)
                        {
                            throw new InvalidOperationException(
                                "Remote content exceeds the size limit.");
                        }

                        memory.Write(
                            buffer,
                            0,
                            read);
                    }

                    return memory.ToArray();
                }
            }
        }

        private static string DecodeUtf8Json(
            byte[] bytes,
            int maxBytes,
            string label)
        {
            if (bytes == null ||
                bytes.Length == 0)
            {
                throw new InvalidOperationException(
                    label + " is empty.");
            }

            if (bytes.LongLength > maxBytes)
                throw new InvalidOperationException(
                    label + " exceeds the size limit.");

            try
            {
                string text =
                    StrictUtf8.GetString(bytes);

                if (text.Length > 0 &&
                    text[0] == '\uFEFF')
                {
                    text = text.Substring(1);
                }

                return text;
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidOperationException(
                    label + " is not valid UTF-8.",
                    ex);
            }
        }

        private void EnsureSnapshotBudget(
            string packageId,
            string commit,
            string targetRelativePath,
            long incomingBytes)
        {
            string root =
                _cache.GetSnapshotRoot(
                    packageId,
                    commit);

            long total = 0;

            if (Directory.Exists(root))
            {
                foreach (string path
                    in Directory.GetFiles(
                        root,
                        "*",
                        SearchOption.AllDirectories))
                {
                    if (path.EndsWith(
                        ".part",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    total +=
                        new FileInfo(path).Length;

                    if (total > SnapshotMaxBytes)
                        break;
                }
            }

            string existing =
                _cache.GetMediaPath(
                    packageId,
                    commit,
                    targetRelativePath);

            if (File.Exists(existing))
                total -= new FileInfo(existing).Length;

            if (incomingBytes < 0 ||
                total + incomingBytes > SnapshotMaxBytes)
            {
                throw new InvalidOperationException(
                    "Mod-page cache snapshot would exceed the total media budget.");
            }
        }

        private static void ValidateImageBytes(
            byte[] bytes,
            string logicalPath,
            int maxBytes)
        {
            if (bytes == null ||
                bytes.Length == 0 ||
                bytes.LongLength > maxBytes)
            {
                throw new InvalidOperationException(
                    "Image exceeds its size limit.");
            }

            string extension =
                Path.GetExtension(logicalPath).ToLowerInvariant();

            int width;
            int height;
            string actualType;

            if (IsPng(bytes))
            {
                actualType = ".png";
                ReadPngDimensions(
                    bytes,
                    out width,
                    out height);
            }
            else if (IsJpeg(bytes))
            {
                actualType = ".jpg";
                ReadJpegDimensions(
                    bytes,
                    out width,
                    out height);
            }
            else
            {
                throw new InvalidOperationException(
                    "Image content is not a supported PNG or JPEG.");
            }

            bool extensionMatches =
                extension == actualType ||
                (actualType == ".jpg" &&
                 extension == ".jpeg");

            if (!extensionMatches)
                throw new InvalidOperationException(
                    "Image extension does not match its file signature.");

            if (width <= 0 ||
                height <= 0 ||
                width > MaxImageDimension ||
                height > MaxImageDimension ||
                ((long)width * (long)height) > MaxImagePixels)
            {
                throw new InvalidOperationException(
                    "Image dimensions exceed the safety limit.");
            }
        }

        private static bool IsPng(byte[] bytes)
        {
            return bytes.Length >= 24 &&
                bytes[0] == 0x89 &&
                bytes[1] == 0x50 &&
                bytes[2] == 0x4E &&
                bytes[3] == 0x47 &&
                bytes[4] == 0x0D &&
                bytes[5] == 0x0A &&
                bytes[6] == 0x1A &&
                bytes[7] == 0x0A;
        }

        private static void ReadPngDimensions(
            byte[] bytes,
            out int width,
            out int height)
        {
            width =
                ReadBigEndianInt32(
                    bytes,
                    16);

            height =
                ReadBigEndianInt32(
                    bytes,
                    20);
        }

        private static bool IsJpeg(byte[] bytes)
        {
            return bytes.Length >= 4 &&
                bytes[0] == 0xFF &&
                bytes[1] == 0xD8;
        }

        private static void ReadJpegDimensions(
            byte[] bytes,
            out int width,
            out int height)
        {
            width = 0;
            height = 0;

            int pos = 2;

            while (pos + 3 < bytes.Length)
            {
                while (pos < bytes.Length &&
                    bytes[pos] != 0xFF)
                {
                    pos++;
                }

                while (pos < bytes.Length &&
                    bytes[pos] == 0xFF)
                {
                    pos++;
                }

                if (pos >= bytes.Length)
                    break;

                int marker = bytes[pos++];

                if (marker == 0xD8 ||
                    marker == 0xD9 ||
                    marker == 0x01 ||
                    (marker >= 0xD0 &&
                     marker <= 0xD7))
                {
                    continue;
                }

                if (pos + 1 >= bytes.Length)
                    break;

                int length =
                    (bytes[pos] << 8) |
                    bytes[pos + 1];

                if (length < 2 ||
                    pos + length > bytes.Length)
                {
                    throw new InvalidOperationException(
                        "Invalid JPEG segment structure.");
                }

                if (IsJpegStartOfFrame(marker))
                {
                    if (length < 7)
                        throw new InvalidOperationException(
                            "Invalid JPEG frame header.");

                    height =
                        (bytes[pos + 3] << 8) |
                        bytes[pos + 4];

                    width =
                        (bytes[pos + 5] << 8) |
                        bytes[pos + 6];

                    return;
                }

                pos += length;
            }

            throw new InvalidOperationException(
                "JPEG dimensions could not be determined safely.");
        }

        private static bool IsJpegStartOfFrame(
            int marker)
        {
            return marker == 0xC0 ||
                marker == 0xC1 ||
                marker == 0xC2 ||
                marker == 0xC3 ||
                marker == 0xC5 ||
                marker == 0xC6 ||
                marker == 0xC7 ||
                marker == 0xC9 ||
                marker == 0xCA ||
                marker == 0xCB ||
                marker == 0xCD ||
                marker == 0xCE ||
                marker == 0xCF;
        }

        private static int ReadBigEndianInt32(
            byte[] bytes,
            int offset)
        {
            if (offset < 0 ||
                offset + 3 >= bytes.Length)
            {
                throw new InvalidOperationException(
                    "Invalid PNG header.");
            }

            uint value =
                ((uint)bytes[offset] << 24) |
                ((uint)bytes[offset + 1] << 16) |
                ((uint)bytes[offset + 2] << 8) |
                bytes[offset + 3];

            if (value > Int32.MaxValue)
                throw new InvalidOperationException(
                    "Image dimension is invalid.");

            return (int)value;
        }

        private static string CombineMessages(
            string first,
            string second)
        {
            if (String.IsNullOrWhiteSpace(first))
                return second == null ? "" : second;

            if (String.IsNullOrWhiteSpace(second))
                return first;

            return first + " " + second;
        }

        private static string SafeMessage(Exception ex)
        {
            if (ex == null ||
                String.IsNullOrWhiteSpace(ex.Message))
            {
                return "unknown error";
            }

            string message =
                ex.Message.Replace(
                    "\r",
                    " ").Replace(
                    "\n",
                    " ").Trim();

            if (message.Length > 600)
                message = message.Substring(0, 600);

            return message;
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
    }
}
