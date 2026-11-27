using System.Text;
using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.Tests;

public class SqlTextTests
{
    // file bytes, configured encoding -> text read
    public static TheoryData<byte[], string, string> Decodable() => new()
    {
        { [], "UTF-8", "" },
        { "SELECT 1"u8.ToArray(), "UTF-8", "SELECT 1" },
        { [0xEF, 0xBB, 0xBF, .. "SELECT 1"u8.ToArray()], "UTF-8", "SELECT 1" },
        // A byte order mark wins over the configured encoding
        { [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("SELECT '売上'")], "Shift-JIS", "SELECT '売上'" },
        { [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("SELECT 1")], "Shift-JIS", "SELECT 1" },
        { [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes("SELECT 1")], "UTF-8", "SELECT 1" },
        { [0xFF, 0xFE, 0x00, 0x00, .. Encoding.UTF32.GetBytes("SELECT 1")], "UTF-8", "SELECT 1" },
        // Without one, the configured encoding decides
        { Encoding.GetEncoding("shift_jis").GetBytes("SELECT '売上'"), "Shift-JIS", "SELECT '売上'" },
    };

    [Theory]
    [MemberData(nameof(Decodable))]
    [Trait("Requirement", "QTC-14")]
    internal void Decode_ValidBytes_ReturnsTheText(byte[] bytes, string encoding, string expected)
    {
        Assert.Equal(expected, SqlText.Decode(bytes, encoding, "q.sql"));
    }

    [Theory]
    [InlineData(new byte[] { 0x53, 0x45, 0xFF, 0x4C }, "UTF-8", "SQL file \"q.sql\" is not valid utf-8 at byte 2.")]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x53, 0xC3 }, "Shift-JIS", "SQL file \"q.sql\" is not valid utf-8 at byte 4.")]
    [InlineData(new byte[] { 0x53, 0x81, 0x20 }, "Shift-JIS", "SQL file \"q.sql\" is not valid shift_jis at byte 1.")]
    [Trait("Requirement", "QTC-14")]
    internal void Decode_InvalidBytes_ReportsTheOffsetInsteadOfReplacing(byte[] bytes, string encoding, string expected)
    {
        RunFailureException failure = Assert.Throws<RunFailureException>(() => SqlText.Decode(bytes, encoding, "q.sql"));

        Assert.Equal(expected, failure.Message);
    }

    [Theory]
    [InlineData(SqlText.MaxCharacters, null)]
    [InlineData(SqlText.MaxCharacters + 1, "SQL text exceeds 1,048,576 characters.")]
    [Trait("Requirement", "QTC-14")]
    internal void LengthError_TextAtAndOverTheLimit_RejectsOnlyOver(int length, string? expected)
    {
        Assert.Equal(expected, SqlText.LengthError(new string('x', length)));
    }

    [Fact]
    [Trait("Requirement", "QTC-14")]
    internal async Task ReadFileAsync_FileOverTheByteLimit_IsRejectedBeforeReading()
    {
        using TempDirectory dir = new();
        string path = Path.Combine(dir.Path, "big.sql");
        await using (FileStream stream = File.Create(path))
            stream.SetLength(SqlText.MaxFileBytes + 1);

        RunFailureException failure = await Assert.ThrowsAsync<RunFailureException>(() => SqlText.ReadFileAsync(path, "UTF-8"));

        Assert.Equal($"SQL file \"{path}\" exceeds 4,194,304 bytes.", failure.Message);
    }

    [Fact]
    [Trait("Requirement", "QTC-14")]
    internal async Task ReadFileAsync_LockedFile_ReportsThatItCouldNotBeRead()
    {
        using TempDirectory dir = new();
        string path = Path.Combine(dir.Path, "locked.sql");
        await File.WriteAllTextAsync(path, "SELECT 1");
        await using FileStream lockHolder = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        RunFailureException failure = await Assert.ThrowsAsync<RunFailureException>(() => SqlText.ReadFileAsync(path, "UTF-8"));

        Assert.Equal($"SQL file \"{path}\" could not be read.", failure.Message);
    }
}
