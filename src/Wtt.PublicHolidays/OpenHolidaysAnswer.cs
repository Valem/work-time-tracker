using System.Globalization;
using System.Text.Json;
using Wtt.Domain.Planning;
using static Wtt.PublicHolidays.OpenHolidays;

namespace Wtt.PublicHolidays;

/// <summary>
/// Reads an OpenHolidays answer strictly into the domain's terms, or refuses it whole. Required fields must be there with
/// their type, other fields are ignored; dates lie inside the requested year, an entry lasts at most 7 days, the answer holds
/// at most 100 entries, each reaching the country or the requested canton, at least one of them a public holiday that counts
/// by itself; a name is at most 100 characters without control or direction-override characters. A reason never quotes
/// the answer.
/// </summary>
internal static class OpenHolidaysAnswer
{
    private const int MaxDaysPerEntry = 7;
    private const int MaxNameLength = 100;

    private static readonly byte[] Utf8ByteOrderMark = [0xEF, 0xBB, 0xBF];

    public static PublicHolidayYear Translate(ReadOnlyMemory<byte> answer, Canton canton, int year, DateTimeOffset loadedAt)
    {
        if (answer.Length > MaxAnswerBytes)
        {
            throw Refused($"The answer is larger than {MaxAnswerBytes / 1024} KiB.");
        }

        using var document = Parse(answer);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw Refused("The answer is not a list of public holidays.");
        }

        var count = root.GetArrayLength();
        if (count > PublicHolidayYear.MaxEntries)
        {
            throw Refused($"The answer holds {count} entries, more than {PublicHolidayYear.MaxEntries}.");
        }

        var entries = new List<PublicHolidayEntry>();
        var number = 0;
        foreach (var item in root.EnumerateArray())
        {
            entries.AddRange(new EntryReader(item, ++number, canton, year).Read());
        }

        if (entries.Count > PublicHolidayYear.MaxEntries)
        {
            throw Refused($"The answer's entries cover {entries.Count} days, more than {PublicHolidayYear.MaxEntries}.");
        }

        if (!entries.Any(entry => entry.CountsByItself))
        {
            throw Refused($"The answer holds no public holiday reaching the country or CH-{canton}.");
        }

        return new PublicHolidayYear(canton, year, Source, loadedAt, entries);
    }

    private static JsonDocument Parse(ReadOnlyMemory<byte> answer)
    {
        var json = answer.Span.StartsWith(Utf8ByteOrderMark) ? answer[Utf8ByteOrderMark.Length..] : answer;
        try
        {
            return JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8, AllowDuplicateProperties = false });
        }
        catch (JsonException failure)
        {
            throw Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"The answer is not JSON or repeats a field (line {(failure.LineNumber ?? 0) + 1}, byte {(failure.BytePositionInLine ?? 0) + 1})."));
        }
    }

    /// <summary>One entry of the answer, numbered from 1 in the reasons it gives.</summary>
    private sealed class EntryReader(JsonElement item, int number, Canton canton, int year)
    {
        private readonly string cantonCode = $"CH-{canton}";

        public IEnumerable<PublicHolidayEntry> Read()
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw Refused($"Entry {number} is not an object.");
            }

            var start = Date("startDate");
            var end = Date("endDate");
            if (end < start)
            {
                throw Refused($"Entry {number} ends before it starts.");
            }

            if (start.Year != year || end.Year != year)
            {
                throw Refused($"Entry {number} lies outside {year}.");
            }

            var days = end.DayNumber - start.DayNumber + 1;
            if (days > MaxDaysPerEntry)
            {
                throw Refused($"Entry {number} lasts {days} days, more than {MaxDaysPerEntry}.");
            }

            var kind = Text("type") switch
            {
                "Public" => HolidayKind.Public,
                "Optional" => HolidayKind.Optional,
                _ => HolidayKind.Other,
            };
            var share = Text("temporalScope") switch
            {
                "FullDay" => DayPart.Whole,
                "HalfDay" => DayPart.Half,
                _ => throw Refused($"Entry {number}'s temporalScope is neither FullDay nor HalfDay."),
            };
            var reach = Reach();
            var names = Names();
            return Enumerable.Range(0, days).Select(day => new PublicHolidayEntry(start.AddDays(day), share, kind, reach, names));
        }

        private HolidayReach Reach()
        {
            if (Required("nationwide") is { ValueKind: JsonValueKind.True })
            {
                return HolidayReach.Country;
            }

            if (Required("nationwide").ValueKind != JsonValueKind.False)
            {
                throw Refused($"Entry {number}'s nationwide is neither true nor false.");
            }

            if (Required("subdivisions") is not { ValueKind: JsonValueKind.Array } subdivisions)
            {
                throw Refused($"Entry {number}'s subdivisions are not a list.");
            }

            var codes = subdivisions.EnumerateArray().Select(subdivision => TextOf(subdivision, "code", "a subdivision's code")).ToList();
            return codes.Contains(cantonCode, StringComparer.Ordinal) ? HolidayReach.Canton
                : codes.Any(code => code.StartsWith(cantonCode + "-", StringComparison.Ordinal)) ? HolidayReach.PartOfCanton
                : throw Refused($"Entry {number} reaches neither the country nor {cantonCode}.");
        }

        private Dictionary<string, string> Names()
        {
            if (Required("name") is not { ValueKind: JsonValueKind.Array } names || names.GetArrayLength() == 0)
            {
                throw Refused($"Entry {number} has no name.");
            }

            var byLanguage = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in names.EnumerateArray())
            {
                var language = TextOf(name, "language", "a name's language");
                if (language.Length != 2 || !language.All(char.IsAsciiLetter))
                {
                    throw Refused($"Entry {number} has a name whose language is not a two-letter code.");
                }

                var text = TextOf(name, "text", "a name's text");
                if (text.Length is 0 or > MaxNameLength)
                {
                    throw Refused($"Entry {number} has a name of {text.Length} characters; a name has 1 to {MaxNameLength}.");
                }

                if (HoldsUnsafeCharacter(text))
                {
                    throw Refused($"Entry {number} has a name holding a control or direction-override character.");
                }

                if (!byLanguage.TryAdd(language.ToUpperInvariant(), text))
                {
                    throw Refused($"Entry {number} names one language twice.");
                }
            }

            return byLanguage;
        }

        private DateOnly Date(string property) =>
            DateOnly.TryParseExact(Text(property), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : throw Refused($"Entry {number}'s {property} is not a date.");

        private string Text(string property) => TextOf(item, property, property);

        private JsonElement Required(string property) =>
            item.TryGetProperty(property, out var value) ? value : throw Refused($"Entry {number} has no {property}.");

        private string TextOf(JsonElement element, string property, string what)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
            {
                throw Refused($"Entry {number} has no {what}.");
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                throw Refused($"Entry {number}'s {what} is not text.");
            }

            try
            {
                return value.GetString()!;
            }
            catch (InvalidOperationException)
            {
                throw Refused($"Entry {number}'s {what} is not valid text.");
            }
        }

        /// <summary>A control character, a mark or override that changes the text's direction, or half a surrogate pair.</summary>
        private static bool HoldsUnsafeCharacter(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (char.IsControl(c) || IsDirectionControl(c))
                {
                    return true;
                }

                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                }
                else if (char.IsSurrogate(c))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDirectionControl(char c) =>
            c is '؜' or '‎' or '‏' or (>= '‪' and <= '‮') or (>= '⁦' and <= '⁩');
    }
}
