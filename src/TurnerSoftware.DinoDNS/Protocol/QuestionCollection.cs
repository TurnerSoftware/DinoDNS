using System.Collections;

namespace TurnerSoftware.DinoDNS.Protocol;

public readonly struct QuestionCollection
{
	private readonly int ItemCount;
	private readonly SeekableReadOnlyMemory<byte> ByteValue;
	private readonly Question[] ArrayValue;
	private readonly bool IsByteSequence;

	public QuestionCollection(Question[] value)
	{
		ItemCount = value.Length;
		ByteValue = ReadOnlyMemory<byte>.Empty;
		ArrayValue = value;
		IsByteSequence = false;
	}

	public QuestionCollection(SeekableReadOnlyMemory<byte> value, int itemCount)
	{
		ItemCount = itemCount;
		ByteValue = value;
		ArrayValue = Array.Empty<Question>();
		IsByteSequence = true;
	}

	public int Count => ItemCount;

	public Enumerator GetEnumerator() => new(this);

	public static implicit operator QuestionCollection(Question[] value) => new(value);

	public struct Enumerator : IEnumerator<Question>
	{
		private readonly QuestionCollection Value;
		private int Index;
		private DnsProtocolReader Reader;

		internal Enumerator(QuestionCollection collection)
		{
			Value = collection;
			Index = 0;
			Current = default;
			Reader = Value.IsByteSequence ? new DnsProtocolReader(Value.ByteValue) : default;
		}

		public Question Current { get; private set; }

		object IEnumerator.Current => Current;

		public bool MoveNext()
		{
			if (Index == Value.ItemCount)
			{
				return false;
			}
			
			if (Value.IsByteSequence)
			{
				Reader = Reader.ReadQuestion(out var question);
				Current = question;
			}
			else
			{
				Current = Value.ArrayValue[Index];
			}

			Index++;
			return true;
		}

		public void Reset()
		{
			Index = 0;
			Current = default;
			Reader = Value.IsByteSequence ? new DnsProtocolReader(Value.ByteValue) : default;
		}

		public void Dispose()
		{
		}
	}
}
