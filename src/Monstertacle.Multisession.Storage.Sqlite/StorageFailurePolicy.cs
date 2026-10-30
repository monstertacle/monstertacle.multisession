using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class StorageFailurePolicy
{
    internal static SessionOperationException Error(ErrorCode code, bool noWrite, bool admission)
        => new(new PublicError(code, noWrite ? ExecutionCertainty.NotStarted : ExecutionCertainty.Unknown,
            AdmissionStatus: admission ? (noWrite ? AdmissionStatus.NotAccepted : AdmissionStatus.Unknown) : null));

    internal static Exception Normalize(Exception failure, bool noWrite, bool admission, CancellationToken token,
        FailureStage stage = FailureStage.StoreExecution)
    {
        if (failure is OperationCanceledException && noWrite)
        {
            return admission ? new AdmissionCancellationException(new(ErrorCode.Cancelled, AdmissionStatus: AdmissionStatus.NotAccepted), token) { Details = new(stage) }
                : new OperationCanceledException(token);
        }
        PublicError error = failure is SessionOperationException known ? known.Failure : new(ErrorCode.StorageUnavailable);
        return new SessionOperationException(error with
        {
            Certainty = noWrite ? ExecutionCertainty.NotStarted : ExecutionCertainty.Unknown,
            AdmissionStatus = admission ? (noWrite ? AdmissionStatus.NotAccepted : AdmissionStatus.Unknown) : null,
        })
        {
            Details = (failure as SessionOperationException)?.Details ?? failure switch
            {
                SqliteException sqlite => new(stage, sqlite.SqliteErrorCode, sqlite.SqliteExtendedErrorCode),
                IOException or UnauthorizedAccessException => new(stage, PlatformCode: failure.HResult & ushort.MaxValue),
                _ => new(stage),
            },
        };
    }
}
