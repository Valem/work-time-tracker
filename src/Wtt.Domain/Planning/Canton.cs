namespace Wtt.Domain.Planning;

/// <summary>The 26 cantons, by their two-letter code.</summary>
public enum Canton
{
    AG,
    AI,
    AR,
    BE,
    BL,
    BS,
    FR,
    GE,
    GL,
    GR,
    JU,
    LU,
    NE,
    NW,
    OW,
    SG,
    SH,
    SO,
    SZ,
    TG,
    TI,
    UR,
    VD,
    VS,
    ZG,
    ZH,
}

public static class Cantons
{
    public static IReadOnlyList<Canton> All { get; } = Enum.GetValues<Canton>();

    /// <summary>The canton with exactly this code — two capital letters, such as ZH — else null.</summary>
    public static Canton? Parse(string? code)
    {
        foreach (var canton in All)
        {
            if (canton.ToString() == code)
            {
                return canton;
            }
        }

        return null;
    }
}
