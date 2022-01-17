using System.Diagnostics;

namespace TurnerSoftware.DinoDNS.Benchmarks;

internal static class ExternalTestServer
{
	private readonly static TimeSpan StartWait = TimeSpan.FromSeconds(1);
	private static Process? Instance;

	public static void StartUdp()
	{
		Instance = Process.Start(new ProcessStartInfo("StaticDnsServer.exe")) ?? throw new Exception("Test server could not start");
		Thread.Sleep(StartWait);
	}
	public static void StartTcp()
	{
		Instance = Process.Start(new ProcessStartInfo("StaticDnsServer.exe") { Arguments = "tcp" }) ?? throw new Exception("Test server could not start");
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
