using System.Buffers.Binary;
using System.Text;

namespace TurnerSoftware.DinoDNS.Protocol;

public ref struct LabelSequenceWriter
{
	private readonly Span<byte> Buffer;
	private readonly int Offset;

	public LabelSequenceWriter(Span<byte> buffer)
	{
		Buffer = buffer;
		Offset = 0;
	}
	internal LabelSequenceWriter(Span<byte> buffer, int offset)
	{
		Buffer = buffer;
		Offset = offset;
	}

	private LabelSequenceWriter Next(int bytesWritten) => new(Buffer, Offset + bytesWritten);

	public LabelSequenceWriter Append(byte value)
	{
		Buffer[Offset] = value;
		return Next(1);
	}

	public LabelSequenceWriter AppendLabel(string value)
	{
		Buffer[Offset] = (byte)value.Length;
		var valueBytesWritten = Encoding.ASCII.GetBytes(value, Buffer[(Offset + 1)..]);
		return Next(1 + valueBytesWritten);
	}

	public LabelSequenceWriter AppendPointer(ushort offset)
	{
		offset |= 0b11000000_00000000;
		BinaryPrimitives.WriteUInt16LittleEndian(Buffer[Offset..], offset);
		return Next(2);
	}

	public byte[] EndSequence() => Append(0).ToArray();

	public byte[] ToArray() => Buffer[0..Offset].ToArray();
}
