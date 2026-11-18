using System.Text;
using System.Text.Json;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>The JSON style of every file the apps write, and the rules for parsing one and its version.</summary>
[Unit]
public sealed class JsonFileTests
{
    private static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;

    // Write

    [Fact]
    public void Writes_two_space_indents_and_line_feed_breaks_with_no_final_line_break()
    {
        var bytes = JsonFile.Write(w =>
        {
            w.WriteStartObject();
            w.WriteStartArray("list");
            w.WriteNumberValue(1);
            w.WriteNumberValue(2);
            w.WriteEndArray();
            w.WriteString("name", "x");
            w.WriteEndObject();
        });

        Assert.Equal("{\n  \"list\": [\n    1,\n    2\n  ],\n  \"name\": \"x\"\n}", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Writes_characters_outside_ASCII_as_they_are()
    {
        var bytes = JsonFile.Write(w =>
        {
            w.WriteStartObject();
            w.WriteString("name", "é日 😀 <&>");
            w.WriteEndObject();
        });

        Assert.Equal("{\n  \"name\": \"é日 😀 <&>\"\n}", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Writes_UTF8_with_no_byte_order_mark()
    {
        var bytes = JsonFile.Write(w => w.WriteStringValue("é"));

        Assert.Equal(new byte[] { (byte)'"', 0xC3, 0xA9, (byte)'"' }, bytes);
    }

    [Fact]
    public void Writes_a_root_array_as_well_as_an_object()
    {
        var bytes = JsonFile.Write(w =>
        {
            w.WriteStartArray();
            w.WriteNumberValue(1);
            w.WriteStringValue("a");
            w.WriteEndArray();
        });

        Assert.Equal("[\n  1,\n  \"a\"\n]", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Rejects_a_write_that_writes_no_value()
    {
        Assert.Throws<InvalidOperationException>(() => JsonFile.Write(_ => { }));
    }

    [Fact]
    public void Rejects_a_write_that_leaves_the_value_unfinished()
    {
        Assert.Throws<InvalidOperationException>(() => JsonFile.Write(w =>
        {
            w.WriteStartObject();
            w.WriteString("a", "b");
        }));
    }

    // Parse

    [Theory]
    [InlineData("{\"a\":1}", JsonValueKind.Object)]
    [InlineData("[1,2]", JsonValueKind.Array)]
    [InlineData("\"text\"", JsonValueKind.String)]
    [InlineData("42", JsonValueKind.Number)]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("null", JsonValueKind.Null)]
    public void Parses_a_valid_JSON_value_of_any_kind(string json, JsonValueKind kind)
    {
        using var document = JsonFile.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Equal(kind, document.RootElement.ValueKind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("not json")]
    [InlineData("{\"a\":1} trailing")]
    public void Rejects_bytes_that_are_not_valid_JSON(string text)
    {
        Assert.Throws<InvalidDataException>(() => JsonFile.Parse(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void Rejects_bytes_that_are_not_valid_UTF8()
    {
        Assert.Throws<InvalidDataException>(() => JsonFile.Parse([0x22, 0xFF, 0xFE, 0x22]));
    }

    [Theory]
    [InlineData(@"""\uD800""")]
    [InlineData(@"""\uDC00""")]
    [InlineData(@"""ab\uD800cd""")]
    [InlineData(@"""\uDE00\uD83D""")]
    [InlineData(@"{ ""a"": [ { ""b"": [ ""x"", ""\uD800"" ] } ] }")]
    [InlineData(@"{ ""a"": [ { ""\uD800"": 1 } ] }")]
    [InlineData(@"{ ""\uDC00"": 1 }")]
    public void Rejects_a_string_value_or_property_name_whose_escapes_make_invalid_text(string json)
    {
        Assert.Throws<InvalidDataException>(() => JsonFile.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Accepts_an_escaped_surrogate_pair_and_reads_it_back_as_the_character()
    {
        using var document = JsonFile.Parse(Encoding.UTF8.GetBytes(@"{ ""a"": [ ""x\uD83D\uDE00"" ], ""\uD83D\uDE00"": 1 }"));

        Assert.Equal("x😀", document.RootElement.GetProperty("a")[0].GetString());
        Assert.True(document.RootElement.TryGetProperty("😀", out _));
    }

    // RequireVersion

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    [InlineData(3, 3)]
    [InlineData(2, 5)]
    public void Returns_a_version_from_1_to_the_newest(int version, int newest)
    {
        Assert.Equal(version, JsonFile.RequireVersion(Root($"{{\"version\":{version}}}"), newest));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"version\"")]
    [InlineData("1")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Version\":1}")]
    [InlineData("{\"version\":\"1\"}")]
    [InlineData("{\"version\":null}")]
    [InlineData("{\"version\":true}")]
    [InlineData("{\"version\":1.5}")]
    [InlineData("{\"version\":0}")]
    [InlineData("{\"version\":-1}")]
    public void Rejects_a_file_that_is_not_an_object_with_a_whole_number_version_from_1(string json)
    {
        Assert.Throws<InvalidDataException>(() => JsonFile.RequireVersion(Root(json), 3));
    }

    [Fact]
    public void Rejects_a_version_above_the_newest()
    {
        Assert.Throws<InvalidDataException>(() => JsonFile.RequireVersion(Root("{\"version\":4}"), 3));
    }

    [Fact]
    public void Reports_a_version_above_the_newest_as_needing_a_newer_app()
    {
        var failure = Assert.Throws<InvalidDataException>(() => JsonFile.RequireVersion(Root("{\"version\":4}"), 3));

        Assert.True(SkippedFile.Of("tasks/a.json", failure).RequiresUpdate);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"version\":0}")]
    [InlineData("{\"version\":1.5}")]
    public void Does_not_report_an_invalid_version_as_needing_a_newer_app(string json)
    {
        var failure = Assert.Throws<InvalidDataException>(() => JsonFile.RequireVersion(Root(json), 3));

        Assert.False(SkippedFile.Of("tasks/a.json", failure).RequiresUpdate);
    }
}
