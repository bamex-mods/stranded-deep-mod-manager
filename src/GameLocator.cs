using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace StrandedDeepModManager
{
    internal static class GameLocator
    {
        public static string FindGameRoot()
        {
            HashSet<string> candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string steamRoot in GetSteamRoots())
            {
                AddCandidate(candidates, Path.Combine(steamRoot, "steamapps", "common", "Stranded Deep"));

                string libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                foreach (string libraryRoot in ReadSteamLibraryRoots(libraryFile))
                {
                    AddCandidate(candidates, Path.Combine(libraryRoot, "steamapps", "common", "Stranded Deep"));
                }
            }

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady)
                        continue;

                    AddCandidate(candidates, Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps", "common", "Stranded Deep"));
                    AddCandidate(candidates, Path.Combine(drive.RootDirectory.FullName, "Steam", "steamapps", "common", "Stranded Deep"));
                }
                catch
                {
                }
            }

            foreach (string candidate in candidates)
            {
                if (LooksLikeGameRoot(candidate))
                    return Path.GetFullPath(candidate);
            }

            return null;
        }

        public static bool LooksLikeGameRoot(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return false;

            string data = Path.Combine(path, "Stranded_Deep_Data");
            string exe = Path.Combine(path, "Stranded_Deep.exe");

            return Directory.Exists(data) || File.Exists(exe);
        }

        private static IEnumerable<string> GetSteamRoots()
        {
            HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddRegistryPath(roots, @"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath");
            AddRegistryPath(roots, @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            AddRegistryPath(roots, @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath");

            return roots;
        }

        private static void AddRegistryPath(HashSet<string> roots, string key, string valueName)
        {
            try
            {
                object value = Registry.GetValue(key, valueName, null);
                string path = value as string;

                if (!String.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    roots.Add(Path.GetFullPath(path));
            }
            catch
            {
            }
        }

        private static IEnumerable<string> ReadSteamLibraryRoots(string libraryFile)
        {
            HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(libraryFile))
                return roots;

            try
            {
                string text = File.ReadAllText(libraryFile);

                MatchCollection pathMatches = Regex.Matches(
                    text,
                    "\\\"path\\\"\\s*\\\"([^\\\"]+)\\\"",
                    RegexOptions.IgnoreCase);

                foreach (Match match in pathMatches)
                {
                    if (match.Groups.Count < 2)
                        continue;

                    string path = UnescapeVdfPath(match.Groups[1].Value);
                    if (Directory.Exists(path))
                        roots.Add(Path.GetFullPath(path));
                }

                MatchCollection legacyMatches = Regex.Matches(
                    text,
                    "\\\"\\d+\\\"\\s*\\\"([^\\\"]+)\\\"");

                foreach (Match match in legacyMatches)
                {
                    if (match.Groups.Count < 2)
                        continue;

                    string path = UnescapeVdfPath(match.Groups[1].Value);
                    if (Directory.Exists(path))
                        roots.Add(Path.GetFullPath(path));
                }
            }
            catch
            {
            }

            return roots;
        }

        private static string UnescapeVdfPath(string value)
        {
            return (value ?? String.Empty).Replace("\\\\", "\\").Replace('/', Path.DirectorySeparatorChar);
        }

        private static void AddCandidate(HashSet<string> candidates, string path)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(path))
                    candidates.Add(Path.GetFullPath(path));
            }
            catch
            {
            }
        }
    }
}
