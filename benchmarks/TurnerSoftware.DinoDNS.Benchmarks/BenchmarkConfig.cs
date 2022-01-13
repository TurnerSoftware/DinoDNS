﻿using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace TurnerSoftware.DinoDNS.Benchmarks;

abstract class BenchmarkConfig : ManualConfig
{
	private const string ENV_ENABLE_HWINTRINSICS = "COMPlus_EnableHWIntrinsic";

	public BenchmarkConfig()
	{
		WithOptions(ConfigOptions.DisableOptimizationsValidator);
		AddDiagnoser(MemoryDiagnoser.Default);
		AddColumn(StatisticColumn.OperationsPerSecond);
	}

	protected void AddCore(bool asBaseline = false)
	{
		AddJob(Job.Default
			.WithRuntime(CoreRuntime.Core60)
			.WithBaseline(asBaseline));
	}

	protected void AddCoreWithoutIntrinsics(bool asBaseline = false)
	{
		AddJob(Job.Default
			.WithRuntime(CoreRuntime.Core60)
			.WithEnvironmentVariable(ENV_ENABLE_HWINTRINSICS, "0")
			.WithBaseline(asBaseline));
	}
}

internal class DefaultBenchmarkConfig : BenchmarkConfig
{
	public DefaultBenchmarkConfig() : base()
	{
		AddCore();
	}
}

internal class IntrinsicBenchmarkConfig : BenchmarkConfig
{
	public IntrinsicBenchmarkConfig() : base()
	{
		AddCore(true);
		AddCoreWithoutIntrinsics();
	}
}