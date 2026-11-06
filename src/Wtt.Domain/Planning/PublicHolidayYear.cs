namespace Wtt.Domain.Planning;

/// <summary>Where public holidays come from, named with the licence of its data.</summary>
public sealed record HolidaySource(string Name, string Licence);

/// <summary>
/// The public holidays of one canton and year as loaded from a source: only dates of the year, at most
/// <see cref="MaxEntries"/> entries and at least one public holiday reaching the country or the canton. Replaced only whole.
/// </summary>
public sealed class PublicHolidayYear
{
    public const int MaxEntries = 100;

    private readonly ILookup<DateOnly, PublicHolidayEntry> byDate;

    public PublicHolidayYear(Canton canton, int year, HolidaySource source, DateTimeOffset loadedAt, IEnumerable<PublicHolidayEntry> entries)
    {
        var ordered = entries.OrderBy(entry => entry.Date).ToList();
        if (ordered.Any(entry => entry.Date.Year != year))
        {
            throw new ArgumentException($"A public-holiday year of {year} holds only dates of {year}.", nameof(entries));
        }

        if (ordered.Count > MaxEntries)
        {
            throw new ArgumentException($"A public-holiday year holds at most {MaxEntries} entries, not {ordered.Count}.", nameof(entries));
        }

        if (!ordered.Any(entry => entry.CountsByItself))
        {
            throw new ArgumentException("A public-holiday year holds at least one public holiday reaching the country or the canton.", nameof(entries));
        }

        Canton = canton;
        Year = year;
        Source = source;
        LoadedAt = loadedAt;
        Entries = ordered;
        byDate = ordered.ToLookup(entry => entry.Date);
    }

    public Canton Canton { get; }

    public int Year { get; }

    public HolidaySource Source { get; }

    public DateTimeOffset LoadedAt { get; }

    public IReadOnlyList<PublicHolidayEntry> Entries { get; }

    public IReadOnlyList<PublicHolidayEntry> On(DateOnly date) => [.. byDate[date]];
}
