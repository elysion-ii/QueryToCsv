namespace QueryToCsv.EndToEndTests;

// What one run of the executable produced.
internal sealed record AppResult(int ExitCode, string Output, string Error);
