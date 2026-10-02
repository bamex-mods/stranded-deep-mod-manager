param(
    [switch]$Run
)

$ErrorActionPreference = "Stop"

$Root = $PSScriptRoot
$Src = Join-Path $Root "src"
$Build = Join-Path $Root "build"
$Assets = Join-Path $Root "assets"

$Csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $Csc)) {
    throw "csc.exe not found: $Csc"
}

$RunningManager = @(
    Get-Process `
        -Name "StrandedDeepModManager" `
        -ErrorAction SilentlyContinue
)

if ($RunningManager.Count -gt 0) {
    $Pids = (
        $RunningManager |
        ForEach-Object { $_.Id }
    ) -join ", "

    throw "Stranded Deep Mod Manager is running (PID: $Pids). Close it before building."
}

Remove-Item -LiteralPath $Build -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $Build | Out-Null

$Sources = @(
    Join-Path $Src "AssemblyInfo.cs"
    Join-Path $Src "AppInfo.cs"
    Join-Path $Src "Program.cs"
    Join-Path $Src "Models.cs"
    Join-Path $Src "JsonUtil.cs"
    Join-Path $Src "GameLocator.cs"
    Join-Path $Src "Assets\AssetKeys.cs"
    Join-Path $Src "UI\Theme.cs"
    Join-Path $Src "UI\DisplayNames.cs"
    Join-Path $Src "UI\UiAssets.cs"
    Join-Path $Src "UI\BorderlessForm.cs"
    Join-Path $Src "UI\StyledVScrollBar.cs"
    Join-Path $Src "UI\ModListViewport.cs"
    Join-Path $Src "UI\ModListItemControl.cs"
    Join-Path $Src "UI\SettingsForm.cs"
    Join-Path $Src "Content\ModPageModels.cs"
    Join-Path $Src "Content\PageCache.cs"
    Join-Path $Src "Content\ModPageService.cs"
    Join-Path $Src "ManagerEngine.cs"
    Join-Path $Src "MainForm.cs"
)

foreach ($Source in $Sources) {
    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Source file missing: $Source"
    }
}

if (-not (Test-Path -LiteralPath $Assets)) {
    throw "Runtime assets directory missing: $Assets"
}

$Exe = Join-Path $Build "StrandedDeepModManager.exe"

$Args = @(
    "/nologo"
    "/target:winexe"
    "/platform:anycpu"
    "/optimize+"
    "/out:$Exe"
    "/reference:System.dll"
    "/reference:System.Core.dll"
    "/reference:System.Drawing.dll"
    "/reference:System.Windows.Forms.dll"
    "/reference:System.Web.Extensions.dll"
    "/reference:System.IO.Compression.dll"
    "/reference:System.IO.Compression.FileSystem.dll"
) + $Sources

Write-Host ""
Write-Host "Building Stranded Deep Mod Manager v0.2.0..."
Write-Host "Compiler: $Csc"
Write-Host "Output:   $Exe"
Write-Host ""

& $Csc $Args

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $Exe)) {
    throw "Build completed without expected EXE."
}

$BuildAssets = Join-Path $Build "assets"
New-Item -ItemType Directory -Force -Path $BuildAssets | Out-Null
Copy-Item -Path (Join-Path $Assets "*") -Destination $BuildAssets -Recurse -Force

Write-Host ""
Write-Host "BUILD OK"
Write-Host $Exe

Get-FileHash -LiteralPath $Exe -Algorithm SHA256

if ($Run) {
    Start-Process -FilePath $Exe
}
