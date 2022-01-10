using System.Diagnostics;

namespace TurnerSoftware.DinoDNS.Benchmarks;

internal static class TestServer
{
	private readonly static TimeSpan StartWait = TimeSpan.FromSeconds(1);
	private static Process? Instance;

	public static void Start()
	{
		Instance = Process.Start(new ProcessStartInfo("StaticDnsServer.exe")) ?? throw new Exception("Test server could not start");
		Thread.Sleep(StartWait);
	}

	public static void Stop()
	{
		if (Instance is not null && !Instance.HasExited)
		{
			Instance.CloseMainWindow();
		}
	}
}
