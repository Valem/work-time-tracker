namespace Wtt.Domain.Planning;

/// <summary>What the source calls the holiday: a public holiday, an optional one, or a kind this program does not know.</summary>
public enum HolidayKind
{
    Public,
    Optional,
    Other,
}

/// <summary>Where the holiday applies within what was requested.</summary>
public enum HolidayReach
{
    Country,
    Canton,
    PartOfCanton,
}

/// <summary>
/// What a tick names: the public-holiday entry of a date with this share. Entries are told apart by share, so a reload of
/// the same year keeps the person's ticks.
/// </summary>
public readonly record struct HolidayTick(DateOnly Date, DayPart Share);

/// <summary>One public holiday on one date, as the source gives it; the names keyed by two-letter language code, upper case.</summary>
public sealed record PublicHolidayEntry(DateOnly Date, DayPart Share, HolidayKind Kind, HolidayReach Reach, IReadOnlyDictionary<string, string> Names)
{
    public const string FallbackLanguage = "DE";

    /// <summary>A public holiday reaching the whole country or the whole canton counts without being ticked.</summary>
    public bool CountsByItself => Kind == HolidayKind.Public && Reach != HolidayReach.PartOfCanton;

    /// <summary>Any other — optional, reaching only part of the canton, or of a kind not known — is offered to tick.</summary>
    public bool Offered => !CountsByItself;

    public HolidayTick Tick => new(Date, Share);

    /// <summary>The name in the language, else in German, else the first the source gave.</summary>
    public string NameIn(string language) =>
        Names.TryGetValue(language, out var name) ? name
        : Names.TryGetValue(FallbackLanguage, out var german) ? german
        : Names.Values.First();
}
