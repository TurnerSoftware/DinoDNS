using TurnerSoftware.DinoDNS.Protocol;
using System.Buffers.Binary;

namespace TurnerSoftware.DinoDNS;

public static class SeekableSpanExtensions
{
	public static SeekableReadOnlySpan<byte> ReadLabelSequence(this SeekableReadOnlySpan<byte> source, out LabelSequence value, out int bytesRead)
	{
		value = LabelSequence.Parse(source, out bytesRead);
		return source.SeekRelative(bytesRead);
	}

	public static SeekableReadOnlySpan<byte> ReadUInt16BigEndian(this SeekableReadOnlySpan<byte> source, out ushort value)
	{
		var result = source.ReadNext(sizeof(ushort), out var slice);
		value = BinaryPrimitives.ReadUInt16BigEndian(slice);
		return result;
	}

	public static SeekableReadOnlySpan<byte> ReadUInt32BigEndian(this SeekableReadOnlySpan<byte> source, out uint value)
	{
		var result = source.ReadNext(sizeof(uint), out var slice);
		value = BinaryPrimitives.ReadUInt32BigEndian(slice);
		return result;
	}
}
