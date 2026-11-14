# Pre-implementation existing-code deep dive

Source ce974f8c435232852b3667dfed273826ff5982ad. Root and a separate read-only
planning researcher examined the current projects and scripts before design.
Common/SessionRuntime composes store, native lease and registered providers;
IStateDirectoryLease, LinuxStateDirectoryLease and WindowsStateDirectoryLease own
native isolation. Storage.Sqlite/SqliteStoreFactory owns open/reopen/preflight.
None needs a new runtime-specific domain branch or state format.

Invoke-Quality/Get-QualitySuites discovers six complete suites. Test-Coverage already
validates the complete native source/module inventory and can consume independent
run directories unchanged. ValidationHostBuild.WorkerPath hardcodes the .NET 10
fallback and must select the matching runtime. Native aliases/project references
in PlatformTests and ValidationHost must cover both new target variants.

Test-PackageLayout currently counts one DLL and one aggregate checkpoint dependency;
both must be checked per target. PackageSmoke, checkpoint/delivery consumers and
publish paths fix .NET 10. Test-ReleaseInput and canonical-consumer expect exactly
one producer manifest. Choose separate OS/runtime CI artifacts, each with one
manifest, and one designated producer for identical package bytes. Preserve past
release records. Existing package versions can be used from net11; no unpublished
SharpQrs/Multisession dependency or unnecessary package upgrade is needed.

Read applicable AGENTS, Constitution, local codemetr-guard skill and existing
coverage-guard guidance. SDKs 11.0.100 and 10.0.403 are installed in a dedicated user
installation. Upstream dotnet/skills revision 2877c712edc20643b72f64e2983b168e6661d607
migration guidance and compiler/core/SDK references were reviewed. No runtime API,
persistence, cancellation, ownership, vendor selection or active process change.
