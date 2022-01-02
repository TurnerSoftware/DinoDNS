using System.Text;

namespace TurnerSoftware.DinoDNS.Protocol;

public ref partial struct LabelSequence
{
	public const ushort PointerFlagByte = 0b11000000;
	public const byte PointerLength = sizeof(ushort);

	private readonly SeekableReadOnlySpan<byte> Value;

	public LabelSequence(SeekableReadOnlySpan<byte> value)
	{
		Value = value;
	}

	public LabelSequenceReader GetReader() => new(Value);

	public void WriteTo(Span<byte> destination, out int bytesWritten)
	{
		GetReader().CountBytes(out bytesWritten, out _);
		Value[..bytesWritten].CopyTo(destination);
	}

	public override string ToString() => GetReader().ReadString();

	public static LabelSequence Parse(SeekableReadOnlySpan<byte> value, out int bytesRead)
	{
		var sequence = new LabelSequence(value);
		sequence.GetReader().CountBytes(out bytesRead, out _);
		return sequence;
	}

	public static LabelSequence Parse(ReadOnlySpan<char> value)
	{
		var fullValue = value;
		var buffer = new byte[value.Length + 2];
		var bufferSlice = (Span<byte>)buffer;
		var count = 0;
		while (value.Length > 0)
		{
			count = value.IndexOf('.');
			if (count == -1)
			{
				count = value.Length;
			}
			
			var valueSlice = value[..count];
			bufferSlice[0] = (byte)count;
			bufferSlice = bufferSlice[1..];
			Encoding.ASCII.GetBytes(value, bufferSlice);
			bufferSlice = bufferSlice[count..];
			value = value[count..];
			if (value.Length > 0 && value[0] == '.')
			{
				value = value[1..];
			}
		}
		bufferSlice[0] = 0;
		return new LabelSequence(buffer.AsSpan());
	}
}
