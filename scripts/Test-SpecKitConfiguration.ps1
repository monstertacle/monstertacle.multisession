[CmdletBinding()]
param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [switch]$ForbidPython
)

# Checks the approved Spec Kit git extension configuration. An extension update or
# reinstall re-enables the hooks and drops Codex skills, so it fails here until the
# resulting diff has been reviewed and the approved configuration restored.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ApprovedExtensionVersion = '1.0.1'
$ApprovedBranchTemplate = 'feature/{number}-{slug}'
$CommandFileByIntegration = @{
    codex = { param($command) ".agents/skills/$($command.Replace('.', '-'))/SKILL.md" }
    opencode = { param($command) ".opencode/commands/$command.md" }
}

$failures = [Collections.Generic.List[string]]::new()
function Read-RequiredText([string]$RelativePath) {
    $path = Join-Path $Root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $failures.Add("Missing $RelativePath.")
        return $null
    }
    return Get-Content -LiteralPath $path -Raw
}

$registryText = Read-RequiredText '.specify/extensions/.registry'
$integrationText = Read-RequiredText '.specify/integration.json'
$hooksText = Read-RequiredText '.specify/extensions.yml'
$configText = Read-RequiredText '.specify/extensions/git/git-config.yml'

if ($null -ne $registryText) {
    $git = (ConvertFrom-Json $registryText -AsHashtable).extensions.git
    if ($null -eq $git) {
        $failures.Add('The git extension is not registered.')
    } else {
        if ($git.version -ne $ApprovedExtensionVersion) {
            $failures.Add("git extension $($git.version) is not the reviewed version $ApprovedExtensionVersion.")
        }
        if ($git.enabled -ne $true) { $failures.Add('The git extension is disabled.') }
        if ($null -ne $integrationText) {
            foreach ($integration in (ConvertFrom-Json $integrationText).installed_integrations) {
                $registered = if ($git.registered_commands -is [Collections.IDictionary]) { $git.registered_commands[$integration] }
                $commands = @($registered | Where-Object { $_ -is [string] -and $_ })
                if (-not $CommandFileByIntegration.ContainsKey($integration)) {
                    $failures.Add("Integration $integration has no reviewed command location.")
                } elseif ($commands.Count -eq 0) {
                    $failures.Add("No git extension commands are registered for $integration.")
                }
                foreach ($command in $commands) {
                    if (-not $CommandFileByIntegration.ContainsKey($integration)) { break }
                    $file = & $CommandFileByIntegration[$integration] $command
                    if (-not (Test-Path -LiteralPath (Join-Path $Root $file) -PathType Leaf)) {
                        $failures.Add("Missing $integration command file $file.")
                    }
                }
            }
        }
    }
}

if ($null -ne $hooksText) {
    $hookEntries = [regex]::Matches($hooksText, '(?m)^\s+- extension:').Count
    $disabled = [regex]::Matches($hooksText, '(?m)^\s+enabled:\s*false\s*$').Count
    $enabledLines = [regex]::Matches($hooksText, '(?m)^\s+enabled:').Count
    if ($hookEntries -eq 0 -or $disabled -ne $hookEntries -or $enabledLines -ne $hookEntries) {
        $failures.Add("All $hookEntries extension hooks must be explicitly disabled; $disabled are.")
    }
}

if ($null -ne $configText) {
    $required = @(
        'branch_numbering: sequential',
        "branch_template: `"$ApprovedBranchTemplate`"",
        'branch_prefix: ""',
        'commit_style: fixed',
        '  default: false')
    foreach ($line in $required) {
        if (-not ($configText -split "`r?`n" -contains $line)) { $failures.Add("git-config.yml lacks '$line'.") }
    }
    if ($configText -match '(?m)^\s+enabled:\s*true') { $failures.Add('An auto-commit event is enabled.') }
    if ($configText -match '\[Spec Kit\]') { $failures.Add('A configured commit message carries a tool prefix.') }
}

if ($ForbidPython) {
    $python = @(& git -C $Root ls-files --cached --others --exclude-standard -- '*.py')
    if ($LASTEXITCODE -ne 0) { $failures.Add('git ls-files failed.') }
    if (Test-Path -LiteralPath (Join-Path $Root '.specify/extensions/git/scripts/python')) {
        $python += '.specify/extensions/git/scripts/python'
    }
    if ($python.Count -gt 0) { $failures.Add("Python files are not allowed: $($python -join ', ').") }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}
Write-Output "Spec Kit git extension configuration passed ($ApprovedExtensionVersion, $ApprovedBranchTemplate, hooks disabled)."
