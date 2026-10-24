using System.Buffers.Binary;

namespace Wtt.Infrastructure.History;

/// <summary>
/// Reads the stop time from event 6008's binary data: two SYSTEMTIME structures, local time then
/// UTC. The text fields hold the same time, but formatted in the machine's language.
/// </summary>
internal static class UnexpectedShutdownTime
{
    private const int SystemTimeLength = 16;
    private const int UtcOffset = SystemTimeLength;

    public static DateTimeOffset? FromBinary(string? hex)
    {
        if (hex is null || hex.Length < 2 * (UtcOffset + SystemTimeLength))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromHexString(hex.AsSpan(0, 2 * (UtcOffset + SystemTimeLength)));
            ushort Field(int index) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(UtcOffset + (2 * index), 2));

            return new DateTimeOffset(
                Field(0), Field(1), Field(3), Field(4), Field(5), Field(6), Field(7), TimeSpan.Zero);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
