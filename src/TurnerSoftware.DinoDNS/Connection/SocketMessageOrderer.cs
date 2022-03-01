using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace TurnerSoftware.DinoDNS.Connection;

internal sealed class SocketMessageOrderer
{
	private readonly static ConcurrentDictionary<IntPtr, ConcurrentDictionary<ushort, Message>> SocketMessages = new();

	private readonly record struct Message(byte[] Data, int MessageLength);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static MessageIdResult CheckMessageId(ReadOnlyMemory<byte> sourceBuffer, ReadOnlyMemory<byte> destinationBuffer)
	{
		const int idSize = sizeof(ushort);
		if (destinationBuffer.Length < idSize)
		{
			return MessageIdResult.Invalid;
		}

		var messagesMatch = sourceBuffer[..idSize].Span.SequenceEqual(destinationBuffer[..idSize].Span);
		return messagesMatch switch
		{
			true => MessageIdResult.Matched,
			_ => MessageIdResult.Mixed
		};
	}

	public static void ClearSocket(Socket socket)
	{
		if (SocketMessages.TryRemove(socket.Handle, out var messageLookup))
		{
			//Any leftover buffers must be returned to the array pool
			foreach (var message in messageLookup.Values)
			{
				ArrayPool<byte>.Shared.Return(message.Data);
			}
			messageLookup.Clear();
		}
	}

	public static int Exchange(
		Socket socket,
		ReadOnlyMemory<byte> sourceBuffer,
		Memory<byte> destinationBuffer,
		int messageLength,
		CancellationToken cancellationToken
	)
	{
		var requestedIdentifier = BinaryPrimitives.ReadUInt16BigEndian(sourceBuffer.Span);
		var receivedIdentifier = BinaryPrimitives.ReadUInt16BigEndian(destinationBuffer.Span);
		var messageLookup = SocketMessages.GetOrAdd(socket.Handle, static _ => new());

		//Use an intermediary rented buffer to avoid use-after-free issues
		var rentedBytes = ArrayPool<byte>.Shared.Rent(messageLength);
		destinationBuffer[..messageLength].CopyTo(rentedBytes.AsMemory());
		messageLookup.TryAdd(receivedIdentifier, new Message(rentedBytes, messageLength));

		var spinWait = new SpinWait();
		do
		{
			if (messageLookup.TryRemove(requestedIdentifier, out var requestedMessage))
			{
				rentedBytes = requestedMessage.Data;
				destinationBuffer.Span.Clear();
				rentedBytes.AsMemory(0, requestedMessage.MessageLength).CopyTo(destinationBuffer);
				ArrayPool<byte>.Shared.Return(rentedBytes);
				return messageLength;
			}

			spinWait.SpinOnce();
		}
		while (!cancellationToken.IsCancellationRequested);
		cancellationToken.ThrowIfCancellationRequested();
		return 0;
	}
}

public enum MessageIdResult
{
	Invalid,
	Mixed,
	Matched
}