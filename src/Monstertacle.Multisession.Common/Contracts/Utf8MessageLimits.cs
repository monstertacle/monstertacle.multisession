using System.Text;

namespace Monstertacle.Multisession.Contracts;

/// <summary>Inclusive UTF-8 boundaries without trimming, truncation or replacement output.</summary>
public static class Utf8MessageLimits
{
    /// <summary>Checks new input before durable acceptance.</summary>
    public static PublicError? ValidateInput(string? input, int maximumBytes)
    {
        if (maximumBytes <= 0)
        {
            return new PublicError(ErrorCode.InvalidConfiguration);
        }

        if (string.IsNullOrEmpty(input))
        {
            return new PublicError(ErrorCode.InvalidInput);
        }

        return Encoding.UTF8.GetByteCount(input) > maximumBytes
            ? new PublicError(ErrorCode.InputLimitExceeded)
            : null;
    }

    /// <summary>Checks final output while preserving the adapter's external termination evidence.</summary>
    public static PublicError? ValidateOutput(string? output, int maximumBytes, ExecutionCertainty certainty)
    {
        if (maximumBytes <= 0)
        {
            return new PublicError(ErrorCode.InvalidConfiguration, certainty);
        }

        if (string.IsNullOrEmpty(output))
        {
            return new PublicError(ErrorCode.ExecutionFailed, certainty);
        }

        return Encoding.UTF8.GetByteCount(output) > maximumBytes
            ? new PublicError(ErrorCode.OutputLimitExceeded, certainty)
            : null;
    }
}
