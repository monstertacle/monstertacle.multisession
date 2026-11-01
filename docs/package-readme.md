# Monstertacle.Multisession

Provider-neutral .NET libraries for running many agent sessions in parallel inside an application process: durable request
identity, SQLite persistence, native Windows and Linux leases, cancellation, stop and explicit recovery. Development release; not
published to a public feed.

## Packages (0.2.0-alpha.7)

| Package | Purpose |
| --- | --- |
| `Monstertacle.Multisession.Common` | Session orchestration contracts, CQRS commands and queries, recovery |
| `Monstertacle.Multisession.Storage.Sqlite` | Durable SQLite store of the current format |
| `Monstertacle.Multisession.Linux` / `Monstertacle.Multisession.Windows` | Native process leases for the platform |
| `Monstertacle.Multisession.AgentFramework` | Integration with Microsoft Agent Framework 1.27.0 |
| `Monstertacle.Multisession.Providers.Conversations` / `.Providers.Interactions` | Optional provider protocol adapters |
| `Monstertacle.Multisession.DeliveryCheckpoints` | Opaque durable delivery checkpoint for one channel to a non-idempotent destination, with atomic receipt and compare-and-swap primitives; it sends nothing itself |

An application references `Common`, `Storage.Sqlite` and exactly one native platform package (`net10.0` on Linux,
`net10.0-windows` on Windows). `DeliveryCheckpoints` is independent of the session packages.

## Documentation

Embedding, operations and the delivery checkpoint contract are documented in the repository:
https://github.com/monstertacle/monstertacle.multisession (`docs/embedding.md`, `docs/operations.md`, `docs/delivery.md`).

## License

MIT (SPDX `MIT`); see the `LICENSE` file in each package. Dependencies are referenced as NuGet packages under their own licenses;
the packages contain no third-party code.
