# Dispatch contract
Public namespace Monstertacle.Multisession.Cqrs. SessionRuntime.Commands/Queries expose SharpQrs execution interfaces after startup. All fourteen routes are registered before exposure; registration interfaces are not exposed even via runtime casts. Registries are runtime-local.
Legacy ports dispatch the corresponding message and preserve ValueTask APIs, absence, original errors, cancellation and pagination. Handlers delegate once to original lifecycle-guarded ports. InspectRecovery is read-only locally and remains tracked during shutdown. No synchronous waiting or service locator.

