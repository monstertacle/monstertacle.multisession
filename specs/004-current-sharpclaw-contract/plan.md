# Implementation Plan
Created: 2026-10-30T07:41:16Z
Owner: Alexander Bleissem
Status: in_progress
SDK10.0.403/.NET10, current SharpClaw source at8ca4862, own six NuGet packages0.2.0-alpha.1.
1. Freeze source contract hashes and opposed-review conclusions.
2. Outside-repository isolated negative/positive process-ownership detector; actual SharpClaw implementation, no live account.
3. Package-only consumer reference with isolated CLI workers and explicit preallocated-ID subset; execute concurrent/follow-up/reopen and negative cases.
4. Document code/ABI/OS boundaries and hashes; disposition unsupported cases as explicit next work, not successful migration.
Constitution: no reverse dependency or host-policy copies, no extra helper/framework, test-first contract gaps, full existing product gates retained. Offline proof is Linux-specific where current SharpClaw Native uses libc/proc; SDK native Windows CI remains a separate requirement.
