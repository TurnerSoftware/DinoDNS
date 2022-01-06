namespace TurnerSoftware.DinoDNS;

/// <summary>
/// </summary>
/// <typeparam name="T"></typeparam>
public struct SeekableMemory<T>
{
	public readonly ReadOnlyMemory<T> Source;
	public readonly int Offset;

	public SeekableMemory(ReadOnlyMemory<T> source)
	{
		Source = source;
		Offset = 0;
	}

	public SeekableMemory(ReadOnlyMemory<T> source, int offset)
	{
		Source = source;
		Offset = offset;
	}

	public bool EndOfSpan => Offset == Source.Length;

	public ReadOnlySpan<T> Span => Source.Span[Offset..];
	public ReadOnlyMemory<T> Memory => Source[Offset..];
	public T this[Index index] => Span[index];
	public ReadOnlyMemory<T> this[Range range] => Memory[range];
	public T Current => this[0];

	public SeekableMemory<T> Seek(int offset) => new(Source, offset);
	public SeekableMemory<T> SeekRelative(int offsetAdjustment) => new(Source, Offset + offsetAdjustment);

	public SeekableMemory<T> Read(out T value)
	{
		value = Current;
		return SeekRelative(1);
	}

	public SeekableMemory<T> ReadNext(int count, out ReadOnlyMemory<T> value)
	{
		value = this[..count];
		return SeekRelative(count);
	}

	/// <summary>
	/// Moves the seekable span by the specified <paramref name="offsetAdjustment"/>.
	/// </summary>
	/// <param name="span"></param>
	/// <param name="offsetAdjustment"></param>
	/// <returns></returns>
	public static SeekableMemory<T> operator +(SeekableMemory<T> span, int offsetAdjustment) => span.SeekRelative(offsetAdjustment);
	public static implicit operator SeekableMemory<T>(Memory<T> source) => new(source);
	public static implicit operator SeekableMemory<T>(ReadOnlyMemory<T> source) => new(source);
	public static implicit operator ReadOnlySpan<T>(SeekableMemory<T> seekableSpan) => seekableSpan.Span;
}