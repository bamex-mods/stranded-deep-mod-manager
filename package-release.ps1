$ErrorActionPreference = "Stop"

$Root = $PSScriptRoot
$BuildScript = Join-Path $Root "build.ps1"
$Exe = Join-Path $Root "build\StrandedDeepModManager.exe"
$Readme = Join-Path $Root "README.md"
$ReleaseRoot = Join-Path $Root "release"
$Stage = Join-Path $ReleaseRoot "StrandedDeepModManager-0.2.0"
$ZipPath = Join-Path $ReleaseRoot "StrandedDeepModManager-0.2.0.zip"
$ShaPath = $ZipPath + ".sha256"

powershell -NoProfile -ExecutionPolicy Bypass -File $BuildScript
if ($LASTEXITCODE -ne 0) {
    throw "build.ps1 failed: $LASTEXITCODE"
}

Remove-Item -LiteralPath $ReleaseRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $Stage | Out-Null

Copy-Item -LiteralPath $Exe -Destination (Join-Path $Stage "StrandedDeepModManager.exe") -Force
Copy-Item -LiteralPath $Readme -Destination (Join-Path $Stage "README.md") -Force

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$fixedTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
$files = Get-ChildItem -LiteralPath $Stage -File | Sort-Object Name

$stream = [System.IO.File]::Open($ZipPath, [System.IO.FileMode]::Create)
try {
    $archive = New-Object -TypeName System.IO.Compression.ZipArchive -ArgumentList @(
        $stream,
        [System.IO.Compression.ZipArchiveMode]::Create,
        $false
    )

    try {
        foreach ($file in $files) {
            $entry = $archive.CreateEntry($file.Name, [System.IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $fixedTime

            $input = [System.IO.File]::OpenRead($file.FullName)
            try {
                $output = $entry.Open()
                try {
                    $input.CopyTo($output)
                }
                finally {
                    $output.Dispose()
                }
            }
            finally {
                $input.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $stream.Dispose()
}

$sha = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$shaLine = "$sha  StrandedDeepModManager-0.2.0.zip`n"
[System.IO.File]::WriteAllText($ShaPath, $shaLine, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ""
Write-Host "PACKAGE OK"
Write-Host "ZIP:    $ZipPath"
Write-Host "SHA256: $sha"
Write-Host "SHA:    $ShaPath"
