using System.Text;

namespace TurnerSoftware.DinoDNS.Protocol;

public readonly partial struct LabelSequence
{
	private readonly SeekableMemory<byte> ByteValue;
	private readonly ReadOnlyMemory<char> CharValue;
	private readonly bool IsByteSequence;

	public LabelSequence(SeekableMemory<byte> value)
	{
		ByteValue = value;
		CharValue = ReadOnlyMemory<char>.Empty;
		IsByteSequence = true;
	}
	public LabelSequence(ReadOnlyMemory<char> value)
	{
		ByteValue = default;
		CharValue = value;
		IsByteSequence = false;
	}
	public LabelSequence(string value) : this(value.AsMemory()) { }

	public Enumerator GetEnumerator() => new(this);

	public int GetSequentialByteLength()
	{
		var sequentialBytes = 0;
		var hasPointer = false;
		foreach (var label in this)
		{
			if (!label.FromPointerOffset)
			{
				//The "+1" is because each label starts with a number that dictates the length
				sequentialBytes += label.Length + 1;
				continue;
			}

			sequentialBytes += PointerLength;
			hasPointer = true;
			break;
		}

		if (!hasPointer)
		{
			//Add the final 0-length label byte - only for non-pointers
			sequentialBytes += 1;
		}
		return sequentialBytes;
	}

	public int GetSequenceByteLength()
	{
		var sequenceLength = 0;
		foreach (var label in this)
		{
			//The "+1" is because each label starts with a number
			sequenceLength += label.Length + 1;
		}
		//Add the final 0-length label byte
		sequenceLength += 1;
		return sequenceLength;
	}

	public override string ToString()
	{
		if (!IsByteSequence)
		{
			return CharValue.ToString();
		}
		else
		{
			var builder = new StringBuilder();
			var isNotFirst = false;
			foreach (var label in this)
			{
				if (isNotFirst)
				{
					builder.Append('.');
				}

				builder.Append(label.ToString());
				isNotFirst = true;
			}
			return builder.ToString();
		}
	}

	public static LabelSequence Parse(SeekableMemory<byte> value, out int bytesRead)
	{
		var sequence = new LabelSequence(value);
		bytesRead = sequence.GetSequentialByteLength();
		return sequence;
	}
}
