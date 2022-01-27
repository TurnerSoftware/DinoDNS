using BenchmarkDotNet.Running;
using TurnerSoftware.DinoDNS.Benchmarks;


// There seems to be a small bug with processing the received data via TCP - pretty sure the issue is with the TcpConnection!

//var a = new FullStackBenchmark();
//a.Setup();
//TestServer.Start(new[] {"tcp"});
//await a.Kapetan_DNS();
//await a.Kapetan_DNS();
//a.Cleanup();

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);