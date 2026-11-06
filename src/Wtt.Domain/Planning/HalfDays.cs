using System.Globalization;

namespace Wtt.Domain.Planning;

/// <summary>An amount of days in whole half days, the planner's only unit: 33 is 16.5 days.</summary>
public readonly record struct HalfDays(int Value) : IComparable<HalfDays>
{
    public static readonly HalfDays Zero = new(0);

    public decimal Days => Value / 2m;

    /// <summary>The amount for a number of days that is a whole multiple of a half day, else null.</summary>
    public static HalfDays? Exactly(decimal days)
    {
        var halves = days * 2;
        return halves == decimal.Truncate(halves) && Math.Abs(halves) <= int.MaxValue ? new HalfDays((int)halves) : null;
    }

    public static HalfDays Min(HalfDays a, HalfDays b) => a.Value <= b.Value ? a : b;

    public static HalfDays operator +(HalfDays a, HalfDays b) => new(a.Value + b.Value);

    public static HalfDays operator -(HalfDays a, HalfDays b) => new(a.Value - b.Value);

    public static bool operator <(HalfDays a, HalfDays b) => a.Value < b.Value;

    public static bool operator >(HalfDays a, HalfDays b) => a.Value > b.Value;

    public static bool operator <=(HalfDays a, HalfDays b) => a.Value <= b.Value;

    public static bool operator >=(HalfDays a, HalfDays b) => a.Value >= b.Value;

    public int CompareTo(HalfDays other) => Value.CompareTo(other.Value);

    /// <summary>The days with a decimal point and at most one place: 16.5, 4, -1.5.</summary>
    public override string ToString() => Days.ToString("0.#", CultureInfo.InvariantCulture);
}
