# Implementation plan

The [Hosting rollout plan](https://github.com/monstertacle/monstertacle.hosting/blob/main/specs/011-staged-secret-protection/plan.md)
defines the common contract. At revision 0ffb107c33afa10e2ae9b2bb35243e3deae3fc19, Constitution
principle V governs credential custody. The `quality` and `codemetr-guard`
workflows invoke `Invoke-Quality.ps1` and `Test-SpecKitConfiguration.ps1`
for native quality and Spec Kit checks. There is no staged scanner in those
entry points or in local contributor tooling. Git's index is the scan input.
The contributor invokes a standalone Bash entry point, which invokes pinned
Gitleaks and emits a redacted status. No library, host or persistent state
depends on this path. Keep existing quality checks in force.

Amend principle V and apply Constitution 3.1.0. Record a failing private probe
for the missing check, implement the script and repeat with clean, staged and
unstaged inputs and a missing dependency. Verify all PR checks at the exact head.
