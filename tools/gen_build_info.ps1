<#
.SYNOPSIS
  Regénère le SHA court de BuildInfo.cs (tampon de version bas-droite + statut Discord).
.DESCRIPTION
  Remplace la constante GitSha entre les marqueurs <AUTOGEN:GITSHA>…</AUTOGEN:GITSHA> par
  le SHA court du HEAD courant. Appelé par tools/release_itch.ps1 avant l'export, pour que
  le tampon corresponde au commit publié. La version, elle, vient de project.godot (lue au runtime).
.NOTES
  Idempotent : si le SHA est déjà à jour, le fichier n'est pas réécrit.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$file = Join-Path $root "src\Core\BuildInfo.cs"

$sha = (git -C $root rev-parse --short HEAD).Trim()
if (-not $sha) { Write-Error "git rev-parse a échoué"; exit 1 }

$content = Get-Content -Raw -Encoding UTF8 $file
$pattern = '(?s)(// <AUTOGEN:GITSHA>.*?public const string GitSha = ")[^"]*(";)'
$replacement = "`${1}$sha`${2}"
$updated = [System.Text.RegularExpressions.Regex]::Replace($content, $pattern, $replacement)

if ($updated -ne $content) {
    Set-Content -Path $file -Value $updated -Encoding UTF8 -NoNewline
    Write-Host "BuildInfo.cs -> GitSha = $sha"
} else {
    Write-Host "BuildInfo.cs déjà à jour (GitSha = $sha)"
}
