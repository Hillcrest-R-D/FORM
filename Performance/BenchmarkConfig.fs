namespace Configs

open BenchmarkDotNet.Configs
open BenchmarkDotNet.Diagnosers
open BenchmarkDotNet.Exporters
open BenchmarkDotNet.Validators
open BenchmarkDotNet.Exporters.Csv

type BenchmarkConfig() as self =

    // Configure your benchmarks, see for more details: https://benchmarkdotnet.org/articles/configs/configs.html.
    inherit ManualConfig() 
    do
        self
            .With(MemoryDiagnoser.Default)
            .With(MarkdownExporter.GitHub)
            .With(ExecutionValidator.FailOnError)
            |> ignore
        self.Add(CsvMeasurementsExporter.Default)
        self.AddDiagnoser(new BenchmarkDotNet.Diagnostics.Windows.EtwProfiler(new BenchmarkDotNet.Diagnostics.Windows.EtwProfilerConfig()))
        self.Add(RPlotExporter.Default)