namespace Monstertacle.Multisession.Contracts;

/// <summary>A preassigned local candidate and its complete unmodified submission.</summary>
/// <param name="RequestId">Candidate generated before entering the commit operation.</param>
/// <param name="Submission">Only the newly submitted request.</param>
public sealed record RequestAcceptance(Guid RequestId, SubmitRequest Submission);
