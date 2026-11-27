namespace QueryToCsv;

// A failure that ends the run with a message the operator can act on. Code that detects
// it throws; the run reports the message on standard error and in the log, and exits 1
// (docs/specs/QueryToCsv.md, QTC-13).
internal sealed class RunFailureException : Exception
{
    internal RunFailureException()
    {
    }

    internal RunFailureException(string message)
        : base(message)
    {
    }

    internal RunFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
