# guard.ps1 — hook PreToolUse : refuse les commandes destructrices ou irreversibles.
# Recoit le JSON du hook sur stdin, repond un JSON de decision sur stdout.
# Complete les regles `deny` de settings.json : celles-ci matchent par prefixe,
# alors que ce script inspecte la ligne de commande entiere (chainages inclus).

$ErrorActionPreference = 'Stop'

function Deny([string]$reason) {
    $payload = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $reason
        }
    }
    $payload | ConvertTo-Json -Depth 5 -Compress
    exit 0
}

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $data = $raw | ConvertFrom-Json } catch { exit 0 }

$cmd = $data.tool_input.command
if ([string]::IsNullOrWhiteSpace($cmd)) { exit 0 }

$projectRoot = 'C:\CODE\JEUX\chimera-protocol'

# Le texte des messages (heredoc, -m "...") n'est pas execute : l'exclure de
# l'analyse, sinon un message de commit qui cite une commande dangereuse bloque.
$cmd = [regex]::Replace($cmd, "(?s)<<-?\s*['`"]?(\w+)['`"]?.*?(\r?\n\1|$)", ' ')
$cmd = [regex]::Replace($cmd, '(?s)(-m|--message)\s+"(?:[^"\\]|\\.)*"', ' ')
$cmd = [regex]::Replace($cmd, "(?s)(-m|--message)\s+'[^']*'", ' ')

# --- Git : reecriture d'historique et pertes de travail non poussees ---------
if ($cmd -match '(?i)git\s+push\b' -and
    $cmd -match '(?i)(--force(?!-with-lease)|\s-f\b)') {
    Deny "git push --force interdit. Utiliser --force-with-lease, ou pousser normalement."
}
if ($cmd -match '(?i)git\s+reset\s+--hard') {
    Deny "git reset --hard interdit : detruit le travail non commite. Utiliser git stash ou git restore <fichier>."
}
if ($cmd -match '(?i)git\s+clean\s+-[a-z]*[fx]') {
    Deny "git clean -f/-x interdit : supprime les fichiers non suivis sans corbeille."
}
if ($cmd -match '(?i)git\s+branch\s+-D\b') {
    Deny "git branch -D interdit : suppression forcee de branche. Utiliser -d (fusionnee uniquement)."
}

# --- Suppressions recursives hors du projet ----------------------------------
$isRecursiveDelete =
    ($cmd -match '(?i)Remove-Item' -and $cmd -match '(?i)-Recurse') -or
    ($cmd -match '(?i)\brm\s+-[a-z]*r') -or
    ($cmd -match '(?i)\brmdir\s+/s') -or
    ($cmd -match '(?i)\brd\s+/s')

if ($isRecursiveDelete) {
    # Les chemins arrivent en backslash (PowerShell), slash (Bash) ou forme MSYS
    # (/c/Users/...). On normalise tout en backslash avant de tester.
    $norm = $cmd -replace '/', '\'
    $norm = [regex]::Replace($norm, '(?<![A-Za-z0-9])\\([A-Za-z])\\', '$1:\')

    # Cibles evidemment interdites : racines, home, dossiers systeme, remontees.
    if ($norm -match '(?i)(\s|["''])[A-Za-z]:\\+(["'']|\s|$)') {
        Deny "Suppression recursive a la racine d'un disque : refusee."
    }
    if ($norm -match '(?i)(\$HOME|%USERPROFILE%|~\\|[A-Za-z]:\\Users\\)') {
        Deny "Suppression recursive visant le dossier utilisateur : refusee."
    }
    if ($norm -match '(?i)[A-Za-z]:\\(Windows|Program Files|ProgramData)') {
        Deny "Suppression recursive visant un dossier systeme : refusee."
    }
    if ($norm -match '\.\.\\') {
        Deny "Suppression recursive avec remontee de repertoire (..) : refusee. Utiliser un chemin explicite."
    }
    # Tout chemin absolu qui sort du projet.
    foreach ($m in [regex]::Matches($norm, '(?i)[A-Za-z]:\\[^\s"'']*')) {
        if (-not $m.Value.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) {
            Deny "Suppression recursive hors du projet ($($m.Value)) : refusee."
        }
    }
}

# --- Destruction de support / effacement securise ----------------------------
if ($cmd -match '(?i)\b(Format-Volume|Clear-Disk|diskpart|cipher\s+/w|Initialize-Disk)\b') {
    Deny "Commande de destruction de support disque : refusee."
}

exit 0
