using System.Text;

namespace Monstertacle.Multisession.Contracts;

/// <summary>Inclusive UTF-8 boundaries without trimming, truncation or replacement output.</summary>
public static class Utf8MessageLimits
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
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

        return ValidateBytes(input, maximumBytes, ErrorCode.InputLimitExceeded, ErrorCode.InvalidInput, ExecutionCertainty.NotStarted);
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

        return ValidateBytes(output, maximumBytes, ErrorCode.OutputLimitExceeded, ErrorCode.ExecutionFailed, certainty);
    }

    private static PublicError? ValidateBytes(string text, int maximumBytes, ErrorCode oversized, ErrorCode malformed, ExecutionCertainty certainty)
    {
        try { return _strictUtf8.GetByteCount(text) > maximumBytes ? new(oversized, certainty) : null; }
        catch (EncoderFallbackException) { return new(malformed, certainty); }
    }
}
