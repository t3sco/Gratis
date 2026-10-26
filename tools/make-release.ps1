# Builds a release zip from a clean checkout of HEAD and writes repo.json (custom Dalamud repository).
# Usage: pwsh tools/make-release.ps1 [-Owner t3sco] [-Repo Gratis] [-Changelog "What changed"]
param([string]$Owner = "t3sco", [string]$Repo = "Gratis", [string]$Changelog = "Early testing build.")
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$work = Join-Path $env:TEMP "gratis-release-$([guid]::NewGuid().ToString('N'))"
git -C $root worktree add $work HEAD --detach | Out-Null
try {
    dotnet build (Join-Path $work "Gratis/Gratis.csproj") -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
    $out = Join-Path $work "Gratis/bin/Release"
    $manifest = Get-Content (Join-Path $out "Gratis.json") -Raw | ConvertFrom-Json
    $version = $manifest.AssemblyVersion
    $dist = Join-Path $root "dist"
    New-Item -ItemType Directory -Force $dist | Out-Null
    $zip = Join-Path $dist "Gratis.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip
    $tag = "v$version"
    $download = "https://github.com/$Owner/$Repo/releases/download/$tag/Gratis.zip"
    $entry = [ordered]@{
        Author = $manifest.Author; Name = $manifest.Name; InternalName = $manifest.InternalName
        AssemblyVersion = $version; Description = $manifest.Description; Punchline = $manifest.Punchline
        ApplicableVersion = "any"; RepoUrl = "https://github.com/$Owner/$Repo"; Tags = $manifest.Tags
        DalamudApiLevel = $manifest.DalamudApiLevel; LoadPriority = 0; AcceptsFeedback = $true
        DownloadLinkInstall = $download; DownloadLinkTesting = $download; DownloadLinkUpdate = $download
        IsHide = $false; IsTestingExclusive = $false
        LastUpdate = [int][DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
        Changelog = $Changelog
    }
    ConvertTo-Json @($entry) -Depth 5 | Set-Content (Join-Path $root "repo.json") -Encoding utf8
    Write-Host "Built $zip (version $version, tag $tag). repo.json updated."
} finally {
    git -C $root worktree remove --force $work
}
