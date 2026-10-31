using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal sealed record ProviderRun(SessionSnapshot Session, IReadOnlyList<string> Inputs, IReadOnlyList<RequestReceipt> Receipts);
