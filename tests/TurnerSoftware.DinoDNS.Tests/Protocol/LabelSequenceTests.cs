using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS.Tests.Protocol;

[TestClass]
public class LabelSequenceTests
{
	public static string ReadLabelsDataNames(MethodInfo _, object[] data) => data[^1].ToString()!;
	public static IEnumerable<object[]> ReadLabelsData()
	{
		var buffer = ArrayPool<byte>.Shared.Rent(512);
		var writer = new LabelSequenceWriter(buffer);

		var singleLabelData = writer.AppendLabel("localhost").EndSequence();
		var multipleLabelData = writer.AppendLabel("example").AppendLabel("org").EndSequence();
		var multipleLabelWithPointersData = writer
				.AppendLabel("www").AppendLabel("example").AppendLabel("org").Append(0)
				.AppendLabel("test").AppendLabel("site").AppendPointer(4).EndSequence();

		ArrayPool<byte>.Shared.Return(buffer);

		yield return new object[]
		{
			singleLabelData,
			"localhost",
			0,
			"Single Label"
		};
		yield return new object[] 
		{
			multipleLabelData,
			"example.org",
			0,
			"Multiple Label"
		};
		yield return new object[]
		{
			multipleLabelWithPointersData,
			"test.site.example.org",
			17,
			"Multiple Label with Pointers"
		};
	}

	[DynamicData(nameof(ReadLabelsData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(ReadLabelsDataNames))]
	[DataTestMethod]
	public void ReadLabels(byte[] data, string expectedDomain, int offset, string testName)
	{
		var a = ReadLabelsData().ToArray();
		var bytes = new SeekableReadOnlySpan<byte>(data.AsSpan()).Seek(offset);
		var reader = LabelSequence.Parse(bytes, out _).GetReader();

		var expectedLabels = expectedDomain.Split('.');
		for (var i = 0; i < expectedLabels.Length; i++)
		{
			var expected = expectedLabels[i];
			reader.Next(out var label, out _);
			var result = Encoding.ASCII.GetString(label);
			Assert.AreEqual(expected, result);
		}

		Assert.IsFalse(reader.Next(out _, out _), "Reader has more labels than expected");
	}
}
