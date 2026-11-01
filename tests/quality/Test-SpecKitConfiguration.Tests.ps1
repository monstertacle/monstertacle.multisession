$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$checker = Join-Path $root 'scripts/Test-SpecKitConfiguration.ps1'
$checkerArguments = @('-ForbidPython')
$temporary = Join-Path $HOME "dev/.agenttemp/speckit-check-tests-$([guid]::NewGuid().ToString('N'))"

function New-Fixture([string]$Name) {
    $fixture = Join-Path $temporary $Name
    New-Item $fixture -ItemType Directory -Force | Out-Null
    & git -C $fixture init -q
    if ($LASTEXITCODE -ne 0) { throw 'git init failed.' }
    Copy-Item (Join-Path $root '.specify') $fixture -Recurse
    foreach ($pattern in @('.agents/skills/speckit-git-*', '.opencode/commands/speckit.git.*')) {
        $target = Join-Path $fixture (Split-Path $pattern -Parent)
        New-Item $target -ItemType Directory -Force | Out-Null
        Copy-Item (Join-Path $root $pattern) $target -Recurse
    }
    return $fixture
}
function Assert-Exit([int]$Expected, [string]$Name, [scriptblock]$Mutation, [string]$Finding = "") {
    $fixture = New-Fixture $Name
    & $Mutation $fixture
    $output = (& (Join-Path $PSHOME "pwsh") -NoProfile -File $checker -Root $fixture @checkerArguments 2>&1 | Out-String)
    $actual = $LASTEXITCODE
    if ($actual -ne $Expected) { throw "Case '$Name' exited $actual instead of $Expected." }
    if ($Finding -and -not $output.Contains($Finding)) { throw "Case '$Name' did not report '$Finding': $output" }
}
function Edit-File([string]$Path, [string]$Pattern, [string]$Replacement) {
    $text = Get-Content -LiteralPath $Path -Raw
    $changed = [regex]::Replace($text, $Pattern, $Replacement, [Text.RegularExpressions.RegexOptions]::Multiline)
    if ($changed -eq $text) { throw "Mutation '$Pattern' did not apply to $Path." }
    Set-Content -LiteralPath $Path -Value $changed -NoNewline
}

try {
    Assert-Exit 0 'approved' { param($f) }
    Assert-Exit 1 'hook-enabled' { param($f) Edit-File "$f/.specify/extensions.yml" '(before_specify:\s+- extension: git\s+command: speckit\.git\.feature\s+enabled: )false' '${1}true' } 'extension hooks must be explicitly disabled'
    Assert-Exit 1 'hook-enabled-field-missing' { param($f) Edit-File "$f/.specify/extensions.yml" '(?m)^    enabled: false\r?\n(?=    optional: false)' '' } 'extension hooks must be explicitly disabled'
    Assert-Exit 1 'codex-skill-missing' { param($f) Remove-Item "$f/.agents/skills/speckit-git-feature" -Recurse } 'Missing codex command file'
    Assert-Exit 1 'codex-registration-missing' { param($f) Edit-File "$f/.specify/extensions/.registry" ',\s*"codex": \[[^\]]*\]' '' } 'No git extension commands are registered for codex'
    Assert-Exit 1 'opencode-command-missing' { param($f) Remove-Item "$f/.opencode/commands/speckit.git.validate.md" } 'Missing opencode command file'
    Assert-Exit 1 'unreviewed-version' { param($f) Edit-File "$f/.specify/extensions/.registry" '"version": "1\.0\.1"' '"version": "1.0.2"' } 'is not the reviewed version'
    Assert-Exit 1 'auto-commit-enabled' { param($f) Edit-File "$f/.specify/extensions/git/git-config.yml" '(after_specify:\s+enabled: )false' '${1}true' } 'An auto-commit event is enabled'
    Assert-Exit 1 'auto-commit-default' { param($f) Edit-File "$f/.specify/extensions/git/git-config.yml" '^  default: false' '  default: true' } 'lacks ''  default: false'''
    Assert-Exit 1 'tool-prefixed-message' { param($f) Edit-File "$f/.specify/extensions/git/git-config.yml" 'message: "Add tasks"' 'message: "[Spec Kit] Add tasks"' } 'carries a tool prefix'
    Assert-Exit 1 'branch-template' { param($f) Edit-File "$f/.specify/extensions/git/git-config.yml" 'branch_template: "feature/\{number\}-\{slug\}"' 'branch_template: ""' } 'lacks ''branch_template'
    Assert-Exit 1 'unreviewed-integration' { param($f) Edit-File "$f/.specify/integration.json" '"installed_integrations": \[' '"installed_integrations": [ "claude",' } 'Integration claude has no reviewed command location'
    Assert-Exit 1 'python-extension-scripts' { param($f) New-Item "$f/.specify/extensions/git/scripts/python" -ItemType Directory | Out-Null; Set-Content "$f/.specify/extensions/git/scripts/python/auto_commit.py" '' } 'Python files are not allowed'
    Assert-Exit 1 'python-file' { param($f) Set-Content "$f/tool.py" '' } 'Python files are not allowed'
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
}
Write-Output 'Spec Kit git extension configuration cases passed.'
