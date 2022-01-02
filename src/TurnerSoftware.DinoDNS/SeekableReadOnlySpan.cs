namespace TurnerSoftware.DinoDNS;

/// <summary>
/// So what I'm thinking is using this as the core type to pass around to my parsing code
/// This allows pointers in labels to jump to arbitrary points (tracking the offset).
/// This will need some polish but the general gist makes sense.
/// </summary>
/// <typeparam name="T"></typeparam>
public ref struct SeekableReadOnlySpan<T>
{
	public readonly ReadOnlySpan<T> Source;
	public readonly int Offset;

	public SeekableReadOnlySpan(ReadOnlySpan<T> source)
	{
		Source = source;
		Offset = 0;
	}

	public SeekableReadOnlySpan(ReadOnlySpan<T> source, int offset)
	{
		Source = source;
		Offset = offset;
	}

	public bool EndOfSpan => Offset == Source.Length;

	public ReadOnlySpan<T> Span => Source[Offset..];
	public T this[Index index] => Span[index];
	public ReadOnlySpan<T> this[Range range] => Span[range];
	public T Current => this[0];

	public SeekableReadOnlySpan<T> Seek(int offset) => new(Source, offset);
	public SeekableReadOnlySpan<T> SeekRelative(int offsetAdjustment) => new(Source, Offset + offsetAdjustment);

	public SeekableReadOnlySpan<T> Read(out T value)
	{
		value = Current;
		return SeekRelative(1);
	}

	public SeekableReadOnlySpan<T> ReadNext(int count, out ReadOnlySpan<T> value)
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
	public static SeekableReadOnlySpan<T> operator +(SeekableReadOnlySpan<T> span, int offsetAdjustment) => span.SeekRelative(offsetAdjustment);
	public static implicit operator SeekableReadOnlySpan<T>(Span<T> source) => new(source);
	public static implicit operator SeekableReadOnlySpan<T>(ReadOnlySpan<T> source) => new(source);
	public static implicit operator ReadOnlySpan<T>(SeekableReadOnlySpan<T> seekableSpan) => seekableSpan.Span;
}