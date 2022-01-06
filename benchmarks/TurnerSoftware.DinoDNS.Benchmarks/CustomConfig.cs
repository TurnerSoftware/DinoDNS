using BenchmarkDotNet.Configs;

namespace TurnerSoftware.DinoDNS.Benchmarks;

internal class CustomConfig : ManualConfig
{
	public CustomConfig()
	{
		WithOptions(ConfigOptions.DisableOptimizationsValidator);
	}
}
