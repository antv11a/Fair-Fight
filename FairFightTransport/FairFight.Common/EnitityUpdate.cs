using System.Buffers.Binary;

namespace FairFight.Common;

/// <summary>
/// Wire layout (40 bytes, little-endian):
///   0  uint32  Sequence      (tick number; per-entity gaps = loss)
///   4  int32   EntityId
///   8  double  X
///  16  double  Y
///  24  double  Z
///  32  int64   SentUtcTicks  (DateTime.UtcNow.Ticks at send time)
/// </summary>
public readonly record struct EntityUpdate(
    uint Sequence, int EntityId, double X, double Y, double Z, long SentUtcTicks)
{
    public const int Size = 40;

    public void WriteTo(Span<byte> b)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(b[0..], Sequence);
        BinaryPrimitives.WriteInt32LittleEndian(b[4..], EntityId);
        BinaryPrimitives.WriteDoubleLittleEndian(b[8..], X);
        BinaryPrimitives.WriteDoubleLittleEndian(b[16..], Y);
        BinaryPrimitives.WriteDoubleLittleEndian(b[24..], Z);
        BinaryPrimitives.WriteInt64LittleEndian(b[32..], SentUtcTicks);
    }

    public static EntityUpdate ReadFrom(ReadOnlySpan<byte> b) => new(
        BinaryPrimitives.ReadUInt32LittleEndian(b[0..]),
        BinaryPrimitives.ReadInt32LittleEndian(b[4..]),
        BinaryPrimitives.ReadDoubleLittleEndian(b[8..]),
        BinaryPrimitives.ReadDoubleLittleEndian(b[16..]),
        BinaryPrimitives.ReadDoubleLittleEndian(b[24..]),
        BinaryPrimitives.ReadInt64LittleEndian(b[32..]));

    /// <summary>
    /// Deterministic test position so the receiver can verify correctness
    /// without any shared state: it recomputes what the sender should have sent.
    /// </summary>
    public static (double X, double Y, double Z) ExpectedPosition(int entityId, uint seq)
        => (100.0 * entityId + 0.5 * seq, 200.0 + entityId, 0.25 * seq);
}