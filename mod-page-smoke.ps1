param(
    [string]$CatalogPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "stranded-deep-mod-catalog\stable\catalog.json")
)

$ErrorActionPreference = "Stop"

$Root = $PSScriptRoot
$Src = Join-Path $Root "src"
$Build = Join-Path $Root "build\mod-page-smoke"
$Csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $CatalogPath)) {
    throw "Catalog not found: $CatalogPath"
}

if (-not (Test-Path -LiteralPath $Csc)) {
    throw "csc.exe not found: $Csc"
}

Remove-Item -LiteralPath $Build -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $Build | Out-Null

$Runner = Join-Path $Build "ModPageSmokeRunner.cs"
$Exe = Join-Path $Build "ModPageSmokeRunner.exe"
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$RunnerSource = @"
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StrandedDeepModManager;
using StrandedDeepModManager.Content;

internal static class ModPageSmokeRunner
{
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static CatalogPackage CloneWithPage(
        CatalogPackage source,
        string commit,
        string path)
    {
        CatalogPackage clone = new CatalogPackage();
        clone.id = source.id;
        clone.name = source.name;
        clone.category = source.category;
        clone.description = source.description;
        clone.repository = source.repository;
        clone.latest = source.latest;
        clone.page = new CatalogPageRef();
        clone.page.repository = source.page.repository;
        clone.page.commit = commit;
        clone.page.path = path;
        return clone;
    }

    public static int Main(string[] args)
    {
        string dataRoot = null;

        try
        {
            if (args.Length != 1)
                throw new InvalidOperationException("Expected catalog path.");

            CatalogRoot catalog =
                JsonUtil.ReadFile<CatalogRoot>(args[0]);

            Assert(catalog != null, "Catalog failed to parse.");
            Assert(catalog.packages != null, "Catalog packages are missing.");

            CatalogPackage package =
                catalog.packages.FirstOrDefault(
                    p => p != null && p.id == "split-map");

            Assert(package != null, "split-map is absent from catalog.");
            Assert(package.page != null, "split-map page reference is absent.");

            dataRoot = Path.Combine(
                Path.GetTempPath(),
                "SDMM-ModPageSmoke-" +
                Guid.NewGuid().ToString("N"));

            PageCache cache = new PageCache(dataRoot);
            ModPageService service = new ModPageService(cache);

            Console.WriteLine("=== ONLINE RU ===");

            ModPageLoadResult ru =
                service.Load(package, "ru");

            Assert(ru != null, "RU page load returned null.");
            Assert(ru.SourceKind == ModPageSourceKind.Online, "RU page was not loaded online.");
            Assert(ru.ResolvedLocale == "ru", "RU locale was not resolved.");
            Assert(ru.Page != null && ru.Page.id == "split-map", "Wrong page id.");
            Assert(ru.Page.highlights != null && ru.Page.highlights.Count == 4, "Expected 4 highlights.");
            Assert(ru.Page.media != null && ru.Page.media.screenshots != null && ru.Page.media.screenshots.Count == 5, "Expected 5 screenshots.");
            Assert(ru.Locale != null && ru.Locale.features != null && ru.Locale.features.Count == 15, "Expected 15 RU features.");
            Assert(ru.Locale.faq != null && ru.Locale.faq.Count == 10, "Expected 10 RU FAQ entries.");
            Assert(!String.IsNullOrWhiteSpace(ru.CoverPath) && File.Exists(ru.CoverPath), "Validated cover is missing.");

            Console.WriteLine("Source:      " + ru.SourceKind);
            Console.WriteLine("Commit:      " + ru.ActualCommit);
            Console.WriteLine("Locale:      " + ru.ResolvedLocale);
            Console.WriteLine("Highlights:  " + ru.Page.highlights.Count);
            Console.WriteLine("Screenshots: " + ru.Page.media.screenshots.Count);
            Console.WriteLine("Features:    " + ru.Locale.features.Count);
            Console.WriteLine("FAQ:         " + ru.Locale.faq.Count);
            Console.WriteLine("Cover:       OK");

            Console.WriteLine();
            Console.WriteLine("=== ONLINE EN ===");

            ModPageLoadResult en =
                service.Load(package, "en");

            Assert(en != null, "EN page load returned null.");
            Assert(en.SourceKind == ModPageSourceKind.Online, "EN page was not loaded online.");
            Assert(en.ResolvedLocale == "en", "EN locale was not resolved.");
            Assert(en.Locale.features != null && en.Locale.features.Count == 15, "Expected 15 EN features.");
            Assert(en.Locale.faq != null && en.Locale.faq.Count == 10, "Expected 10 EN FAQ entries.");

            Console.WriteLine("Source:   " + en.SourceKind);
            Console.WriteLine("Locale:   " + en.ResolvedLocale);
            Console.WriteLine("Features: " + en.Locale.features.Count);
            Console.WriteLine("FAQ:      " + en.Locale.faq.Count);

            Console.WriteLine();
            Console.WriteLine("=== LAZY SCREENSHOT ===");

            string screenshot =
                service.EnsureScreenshot(
                    package,
                    ru,
                    "overview");

            Assert(File.Exists(screenshot), "Lazy screenshot was not cached.");
            Console.WriteLine("Screenshot: OK");

            Console.WriteLine();
            Console.WriteLine("=== EXACT OFFLINE CACHE ===");

            service.NetworkEnabled = false;

            ModPageLoadResult cached =
                service.Load(package, "ru");

            Assert(cached.SourceKind == ModPageSourceKind.ExactCache, "Expected exact cache fallback.");
            Assert(cached.ResolvedLocale == "ru", "Cached RU locale failed.");

            string cachedScreenshot =
                service.EnsureScreenshot(
                    package,
                    cached,
                    "overview");

            Assert(File.Exists(cachedScreenshot), "Cached screenshot failed.");

            Console.WriteLine("Source:     " + cached.SourceKind);
            Console.WriteLine("Locale:     " + cached.ResolvedLocale);
            Console.WriteLine("Screenshot: cached OK");

            Console.WriteLine();
            Console.WriteLine("=== LAST-GOOD OFFLINE CACHE ===");

            CatalogPackage future =
                CloneWithPage(
                    package,
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    package.page.path);

            ModPageLoadResult lastGood =
                service.Load(future, "ru");

            Assert(lastGood.SourceKind == ModPageSourceKind.LastGoodCache, "Expected last-good fallback.");
            Assert(lastGood.ActualCommit == package.page.commit, "Last-good cache resolved the wrong commit.");

            Console.WriteLine("Source: " + lastGood.SourceKind);
            Console.WriteLine("Actual: " + lastGood.ActualCommit);

            Console.WriteLine();
            Console.WriteLine("=== UNSAFE PATH REJECTION ===");

            CatalogPackage unsafePackage =
                CloneWithPage(
                    package,
                    package.page.commit,
                    "../page.json");

            bool unsafeRejected = false;

            try
            {
                service.Load(unsafePackage, "ru");
            }
            catch (InvalidOperationException)
            {
                unsafeRejected = true;
            }

            Assert(unsafeRejected, "Unsafe page path was not rejected.");

            Console.WriteLine("Unsafe path: REJECTED");

            Console.WriteLine();
            Console.WriteLine("=== NO-PAGE FALLBACK ===");

            CatalogPackage noPage =
                new CatalogPackage();

            noPage.id = "no-page";
            noPage.name = "No Page";

            ModPageLoadResult missing =
                service.Load(noPage, "ru");

            Assert(missing == null, "Package without page should return null.");

            Console.WriteLine("No page: graceful null");

            Console.WriteLine();
            Console.WriteLine("MOD PAGE SMOKE OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("MOD PAGE SMOKE FAILED");
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            if (!String.IsNullOrWhiteSpace(dataRoot))
            {
                try
                {
                    if (Directory.Exists(dataRoot))
                        Directory.Delete(dataRoot, true);
                }
                catch
                {
                }
            }
        }
    }
}
"@

[System.IO.File]::WriteAllText(
    $Runner,
    ($RunnerSource.Replace("`r`n", "`n").TrimEnd() + "`n"),
    $Utf8NoBom
)

$Sources = @(
    Join-Path $Src "AppInfo.cs"
    Join-Path $Src "Models.cs"
    Join-Path $Src "JsonUtil.cs"
    Join-Path $Src "Content\ModPageModels.cs"
    Join-Path $Src "Content\PageCache.cs"
    Join-Path $Src "Content\ModPageService.cs"
    $Runner
)

$Args = @(
    "/nologo"
    "/target:exe"
    "/platform:anycpu"
    "/optimize+"
    "/out:$Exe"
    "/reference:System.dll"
    "/reference:System.Core.dll"
    "/reference:System.Web.Extensions.dll"
) + $Sources

Write-Host ""
Write-Host "Compiling ModPage smoke runner..."

& $Csc $Args

if ($LASTEXITCODE -ne 0) {
    throw "Smoke runner compilation failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Running public Split Map page smoke test..."
Write-Host ""

& $Exe $CatalogPath

if ($LASTEXITCODE -ne 0) {
    throw "ModPage smoke runner failed with exit code $LASTEXITCODE"
}
