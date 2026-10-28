namespace Monstertacle.Multisession.Contracts;

/// <summary>Immutable input and confirmed context captured atomically with a dispatch reservation.</summary>
/// <param name="Reservation">The already committed reservation.</param>
/// <param name="Session">Session context frozen when the reservation was acquired.</param>
/// <param name="Submission">Complete, unchanged accepted submission.</param>
/// <param name="Sequence">Accepted session sequence.</param>
/// <param name="BindingRevision">Confirmed continuation revision used by this attempt.</param>
public sealed record ReservedDispatch(
    ExecutionReservation Reservation,
    SessionSnapshot Session,
    SubmitRequest Submission,
    long Sequence,
    long BindingRevision);
