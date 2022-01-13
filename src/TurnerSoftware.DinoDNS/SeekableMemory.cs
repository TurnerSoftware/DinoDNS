namespace TurnerSoftware.DinoDNS;

/// <summary>
/// </summary>
/// <typeparam name="T"></typeparam>
public readonly struct SeekableMemory<T>
{
	public readonly Memory<T> Source;
	public readonly int Offset;

	public SeekableMemory(Memory<T> source)
	{
		Source = source;
		Offset = 0;
	}

	public SeekableMemory(Memory<T> source, int offset)
	{
		Source = source;
		Offset = offset;
	}

	public bool EndOfData => Offset == Source.Length;
	public int Remaining => Source.Length - Offset;

	public Span<T> Span => Source.Span[Offset..];
	public Memory<T> Memory => Source[Offset..];
	public T this[Index index] => Span[index];
	public Memory<T> this[Range range] => Memory[range];
	public T Current => this[0];

	public SeekableMemory<T> Seek(int offset) => new(Source, offset);
	public SeekableMemory<T> SeekRelative(int offsetAdjustment) => new(Source, Offset + offsetAdjustment);

	public SeekableMemory<T> Read(out T value)
	{
		value = Current;
		return SeekRelative(1);
	}

	public SeekableMemory<T> ReadNext(int count, out Memory<T> value)
	{
		value = this[..count];
		return SeekRelative(count);
	}

	public static implicit operator SeekableMemory<T>(Memory<T> source) => new(source);
}

public readonly struct SeekableReadOnlyMemory<T>
{
	public readonly ReadOnlyMemory<T> Source;
	public readonly int Offset;

	public SeekableReadOnlyMemory(ReadOnlyMemory<T> source)
	{
		Source = source;
		Offset = 0;
	}

	public SeekableReadOnlyMemory(ReadOnlyMemory<T> source, int offset)
	{
		Source = source;
		Offset = offset;
	}

	public bool EndOfData => Offset == Source.Length;
	public int Remaining => Source.Length - Offset;

	public ReadOnlySpan<T> Span => Source.Span[Offset..];
	public ReadOnlyMemory<T> Memory => Source[Offset..];
	public T this[Index index] => Span[index];
	public SeekableReadOnlyMemory<T> this[Range range] => Memory[range];
	public T Current => this[0];

	public SeekableReadOnlyMemory<T> Seek(int offset) => new(Source, offset);
	public SeekableReadOnlyMemory<T> SeekRelative(int offsetAdjustment) => new(Source, Offset + offsetAdjustment);

	public SeekableReadOnlyMemory<T> Read(out T value)
	{
		value = Current;
		return SeekRelative(1);
	}

	public SeekableReadOnlyMemory<T> ReadNext(int count, out ReadOnlyMemory<T> value)
	{
		value = this[..count];
		return SeekRelative(count);
	}

	public static implicit operator SeekableReadOnlyMemory<T>(Memory<T> source) => new(source);
	public static implicit operator SeekableReadOnlyMemory<T>(ReadOnlyMemory<T> source) => new(source);
	public static implicit operator ReadOnlySpan<T>(SeekableReadOnlyMemory<T> source) => source.Span;
	public static implicit operator ReadOnlyMemory<T>(SeekableReadOnlyMemory<T> source) => source.Memory;
}