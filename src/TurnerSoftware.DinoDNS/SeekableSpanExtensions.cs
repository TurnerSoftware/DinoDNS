using TurnerSoftware.DinoDNS.Protocol;
using System.Buffers.Binary;

namespace TurnerSoftware.DinoDNS;

public static class SeekableSpanExtensions
{
	public static SeekableMemory<byte> ReadLabelSequence(this SeekableMemory<byte> source, out LabelSequence value, out int bytesRead)
	{
		value = LabelSequence.Parse(source, out bytesRead);
		return source.SeekRelative(bytesRead);
	}

	public static SeekableMemory<byte> ReadUInt16BigEndian(this SeekableMemory<byte> source, out ushort value)
	{
		var result = source.ReadNext(sizeof(ushort), out var slice);
		value = BinaryPrimitives.ReadUInt16BigEndian(slice.Span);
		return result;
	}

	public static SeekableMemory<byte> ReadUInt32BigEndian(this SeekableMemory<byte> source, out uint value)
	{
		var result = source.ReadNext(sizeof(uint), out var slice);
		value = BinaryPrimitives.ReadUInt32BigEndian(slice.Span);
		return result;
	}
}
