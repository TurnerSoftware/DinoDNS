using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;

namespace TurnerSoftware.DinoDNS.Benchmarks;

internal class CustomConfig : ManualConfig
{
	public CustomConfig()
	{
		WithOptions(ConfigOptions.DisableOptimizationsValidator);
		AddDiagnoser(MemoryDiagnoser.Default);
		AddColumn(StatisticColumn.OperationsPerSecond);
	}
}
