namespace Configs

open BenchmarkDotNet.Configs
open BenchmarkDotNet.Diagnosers
open BenchmarkDotNet.Exporters
open BenchmarkDotNet.Validators
open BenchmarkDotNet.Exporters.Csv

type BenchmarkConfig() as this =

    // Configure your benchmarks, see for more details: https://benchmarkdotnet.org/articles/configs/configs.html.
    inherit ManualConfig() 
    do
        this.AddExporter(MarkdownExporter.GitHub).AddExporter(CsvMeasurementsExporter.Default).AddExporter(RPlotExporter.Default)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddValidator(ExecutionValidator.FailOnError)
            |> ignore