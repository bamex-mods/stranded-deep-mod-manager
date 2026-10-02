using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StrandedDeepModManager.Content
{
    public sealed class PageCache
    {
        private static readonly Regex CacheKeyPattern =
            new Regex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant);

        private static readonly Regex CommitPattern =
            new Regex("^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant);

        private readonly string _pagesRoot;

        public PageCache(string dataRoot)
        {
            if (String.IsNullOrWhiteSpace(dataRoot))
                throw new ArgumentException("dataRoot is required.", "dataRoot");

            _pagesRoot = Path.Combine(
                Path.GetFullPath(dataRoot),
                "cache",
                "pages");

            Directory.CreateDirectory(_pagesRoot);
        }

        public string PagesRoot
        {
            get { return _pagesRoot; }
        }

        public string GetSnapshotRoot(string packageId, string commit)
        {
            ValidateCacheKey(packageId, "package id");
            ValidateCommit(commit);

            return Path.Combine(
                _pagesRoot,
                packageId,
                commit.ToLowerInvariant());
        }

        public bool TryReadPageBytes(
            string packageId,
            string commit,
            out byte[] bytes)
        {
            string path = Path.Combine(
                GetSnapshotRoot(packageId, commit),
                "page.json");

            return TryReadBytes(path, out bytes);
        }

        public bool TryReadLocaleBytes(
            string packageId,
            string commit,
            string locale,
            out byte[] bytes)
        {
            ValidateCacheKey(locale, "locale");

            string path = Path.Combine(
                GetSnapshotRoot(packageId, commit),
                "locales",
                locale + ".json");

            return TryReadBytes(path, out bytes);
        }

        public string GetMediaPath(
            string packageId,
            string commit,
            string relativeCachePath)
        {
            string root = GetSnapshotRoot(packageId, commit);
            string safe = NormalizeRelativeCachePath(relativeCachePath);
            string full = Path.GetFullPath(Path.Combine(root, safe));

            string rootWithSeparator =
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            if (!full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cache media path escapes the snapshot root.");

            return full;
        }

        public bool TryReadMediaBytes(
            string packageId,
            string commit,
            string relativeCachePath,
            out byte[] bytes)
        {
            string path = GetMediaPath(
                packageId,
                commit,
                relativeCachePath);

            return TryReadBytes(path, out bytes);
        }

        public void StoreCore(
            PageCachePointer pointer,
            string locale,
            byte[] pageBytes,
            byte[] localeBytes)
        {
            ValidatePointer(pointer);
            ValidateCacheKey(locale, "locale");

            if (pageBytes == null || pageBytes.Length == 0)
                throw new ArgumentException("pageBytes are required.", "pageBytes");

            if (localeBytes == null || localeBytes.Length == 0)
                throw new ArgumentException("localeBytes are required.", "localeBytes");

            string packageRoot = Path.Combine(_pagesRoot, pointer.id);
            Directory.CreateDirectory(packageRoot);

            string target = GetSnapshotRoot(pointer.id, pointer.commit);
            string pagePath = Path.Combine(target, "page.json");
            string localePath = Path.Combine(target, "locales", locale + ".json");

            if (!Directory.Exists(target))
            {
                string staging = Path.Combine(
                    packageRoot,
                    pointer.commit.ToLowerInvariant() +
                    ".staging-" +
                    Guid.NewGuid().ToString("N"));

                try
                {
                    Directory.CreateDirectory(Path.Combine(staging, "locales"));

                    File.WriteAllBytes(
                        Path.Combine(staging, "page.json"),
                        pageBytes);

                    File.WriteAllBytes(
                        Path.Combine(staging, "locales", locale + ".json"),
                        localeBytes);

                    if (!Directory.Exists(target))
                    {
                        Directory.Move(staging, target);
                    }
                }
                finally
                {
                    TryDeleteDirectory(staging);
                }
            }
            else
            {
                byte[] existingPage;

                if (!TryReadBytes(pagePath, out existingPage) ||
                    !BytesEqual(existingPage, pageBytes))
                {
                    AtomicWriteBytes(pagePath, pageBytes);
                }

                byte[] existingLocale;

                if (!TryReadBytes(localePath, out existingLocale) ||
                    !BytesEqual(existingLocale, localeBytes))
                {
                    AtomicWriteBytes(localePath, localeBytes);
                }
            }

            WriteLastGood(pointer);
        }

        public void StoreMedia(
            string packageId,
            string commit,
            string relativeCachePath,
            byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Media bytes are required.", "bytes");

            string path = GetMediaPath(
                packageId,
                commit,
                relativeCachePath);

            AtomicWriteBytes(path, bytes);
        }

        public bool TryReadLastGood(
            string packageId,
            out PageCachePointer pointer)
        {
            ValidateCacheKey(packageId, "package id");

            pointer = null;

            string path = Path.Combine(
                _pagesRoot,
                packageId,
                "last-good.json");

            if (!File.Exists(path))
                return false;

            try
            {
                PageCachePointer loaded =
                    JsonUtil.ReadFile<PageCachePointer>(path);

                ValidatePointer(loaded);

                if (!String.Equals(
                    loaded.id,
                    packageId,
                    StringComparison.Ordinal))
                {
                    return false;
                }

                if (!Directory.Exists(
                    GetSnapshotRoot(loaded.id, loaded.commit)))
                {
                    return false;
                }

                pointer = loaded;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void WriteLastGood(PageCachePointer pointer)
        {
            ValidatePointer(pointer);

            string packageRoot = Path.Combine(
                _pagesRoot,
                pointer.id);

            Directory.CreateDirectory(packageRoot);

            string target = Path.Combine(
                packageRoot,
                "last-good.json");

            string temp = target + ".part-" + Guid.NewGuid().ToString("N");

            try
            {
                JsonUtil.WriteFile(temp, pointer);
                ReplaceFile(temp, target);
            }
            finally
            {
                TryDeleteFile(temp);
            }
        }

        private static bool TryReadBytes(
            string path,
            out byte[] bytes)
        {
            bytes = null;

            try
            {
                if (!File.Exists(path))
                    return false;

                bytes = File.ReadAllBytes(path);
                return true;
            }
            catch
            {
                bytes = null;
                return false;
            }
        }

        private static void AtomicWriteBytes(
            string destination,
            byte[] bytes)
        {
            string dir = Path.GetDirectoryName(destination);

            if (!String.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string temp =
                destination +
                ".part-" +
                Guid.NewGuid().ToString("N");

            try
            {
                File.WriteAllBytes(temp, bytes);
                ReplaceFile(temp, destination);
            }
            finally
            {
                TryDeleteFile(temp);
            }
        }

        private static void ReplaceFile(
            string source,
            string destination)
        {
            string dir = Path.GetDirectoryName(destination);

            if (!String.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(destination))
            {
                File.Replace(
                    source,
                    destination,
                    null,
                    true);

                return;
            }

            File.Move(source, destination);
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (Object.ReferenceEquals(a, b))
                return true;

            if (a == null || b == null || a.Length != b.Length)
                return false;

            return a.SequenceEqual(b);
        }

        private static void ValidatePointer(PageCachePointer pointer)
        {
            if (pointer == null)
                throw new InvalidOperationException("Page cache pointer is missing.");

            if (pointer.schemaVersion != 1)
                throw new InvalidOperationException("Unsupported page cache pointer schema.");

            ValidateCacheKey(pointer.id, "package id");
            ValidateCacheKey(pointer.repository, "repository");
            ValidateCommit(pointer.commit);

            if (String.IsNullOrWhiteSpace(pointer.path))
                throw new InvalidOperationException("Page cache pointer path is missing.");
        }

        private static void ValidateCacheKey(
            string value,
            string label)
        {
            if (String.IsNullOrWhiteSpace(value) ||
                !CacheKeyPattern.IsMatch(value))
            {
                throw new InvalidOperationException(
                    "Invalid " + label + " for page cache.");
            }
        }

        private static void ValidateCommit(string commit)
        {
            if (String.IsNullOrWhiteSpace(commit) ||
                !CommitPattern.IsMatch(commit))
            {
                throw new InvalidOperationException(
                    "Invalid immutable page commit for page cache.");
            }
        }

        private static string NormalizeRelativeCachePath(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Cache relative path is empty.");

            string value = path.Replace('/', Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(value) ||
                value.IndexOf(':') >= 0)
            {
                throw new InvalidOperationException("Unsafe cache relative path.");
            }

            string[] parts = value.Split(
                new char[] {
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                },
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                throw new InvalidOperationException("Unsafe cache relative path.");

            foreach (string part in parts)
            {
                if (part == "." || part == "..")
                    throw new InvalidOperationException("Unsafe cache relative path.");
            }

            return String.Join(
                Path.DirectorySeparatorChar.ToString(),
                parts);
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
    }
}
