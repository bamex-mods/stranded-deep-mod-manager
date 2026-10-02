param(
    [string]$GameRoot,
    [switch]$Online
)

$ErrorActionPreference = "Stop"

$Root = $PSScriptRoot
$Exe = Join-Path $Root "build\StrandedDeepModManager.exe"
$CatalogUrl = "https://raw.githubusercontent.com/bamex-mods/stranded-deep-mod-catalog/main/stable/catalog.json"

if (-not (Test-Path -LiteralPath $Exe)) {
    throw "Manager EXE not found. Run build.ps1 first."
}

$sourceFiles = Get-ChildItem -LiteralPath $Root -File -Recurse |
    Where-Object {
        $_.FullName -notlike '*\build\*' -and
        $_.FullName -notlike '*\release\*' -and
        $_.Name -ne 'smoke-check.ps1' -and
        $_.Extension -in '.cs','.ps1','.md','.json'
    } |
    Select-Object -ExpandProperty FullName

$hits = @(
    Select-String `
        -Path $sourceFiles `
        -Pattern 'F:\\mod-work|ai_share|192\.168\.|deploy-from-mac|\bssh\b|\bscp\b' `
        -CaseSensitive:$false `
        -ErrorAction SilentlyContinue
)

if ($hits.Count -gt 0) {
    $hits | Select-Object Path, LineNumber, Line
    throw "Public-source safety scan failed."
}

if (-not [String]::IsNullOrWhiteSpace($GameRoot)) {
    if (-not (Test-Path -LiteralPath $GameRoot)) {
        throw "GameRoot does not exist: $GameRoot"
    }

    if (-not (
        (Test-Path -LiteralPath (Join-Path $GameRoot 'Stranded_Deep_Data')) -or
        (Test-Path -LiteralPath (Join-Path $GameRoot 'Stranded_Deep.exe'))
    )) {
        throw "GameRoot does not look like Stranded Deep: $GameRoot"
    }
}

if ($Online) {
    $tmp = Join-Path $env:TEMP ("sdmm-catalog-" + [guid]::NewGuid().ToString('N') + '.json')
    $tmpSha = $tmp + '.sha256'

    try {
        $headers = @{
            'User-Agent' = 'StrandedDeepModManager-SmokeCheck/0.2.0'
            'Accept'     = 'application/vnd.github+json'
        }

        $refUrl = 'https://api.github.com/repos/bamex-mods/stranded-deep-mod-catalog/git/ref/heads/main'
        $ref = Invoke-RestMethod -UseBasicParsing -Headers $headers -Uri $refUrl
        $commit = [string]$ref.object.sha

        if ($commit -notmatch '^[0-9a-fA-F]{40}([0-9a-fA-F]{24})?$') {
            throw "GitHub returned an invalid catalog commit SHA: $commit"
        }

        $snapshotUrl = "https://raw.githubusercontent.com/bamex-mods/stranded-deep-mod-catalog/$commit/stable/catalog.json"

        Invoke-WebRequest -UseBasicParsing -Headers @{ 'User-Agent' = $headers['User-Agent'] } -Uri $snapshotUrl -OutFile $tmp
        Invoke-WebRequest -UseBasicParsing -Headers @{ 'User-Agent' = $headers['User-Agent'] } -Uri ($snapshotUrl + '.sha256') -OutFile $tmpSha

        $expected = ((Get-Content -LiteralPath $tmpSha -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
        $actual = (Get-FileHash -LiteralPath $tmp -Algorithm SHA256).Hash.ToLowerInvariant()

        if ($actual -ne $expected) {
            throw "Online stable catalog SHA mismatch at commit $commit."
        }

        $catalog = Get-Content -LiteralPath $tmp -Raw | ConvertFrom-Json
        if ($catalog.schemaVersion -ne 1) {
            throw "Unexpected catalog schemaVersion."
        }

        if (@($catalog.packages).Count -ne 7) {
            throw "Expected 7 public stable packages, found $(@($catalog.packages).Count)."
        }

        Write-Host "Catalog commit: $commit"
        Write-Host "Catalog SHA256: $actual"
    }
    finally {
        Remove-Item -LiteralPath $tmp, $tmpSha -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "SMOKE CHECK OK"
Write-Host "EXE:     $Exe"
Write-Host "SHA256:  $((Get-FileHash -LiteralPath $Exe -Algorithm SHA256).Hash)"
Write-Host "Catalog: $CatalogUrl"
Write-Host ""
