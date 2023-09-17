It's with great pleasure we announce Form 2.0. This release contains 3 major features: 

- Transaction Support
- A Refactor
- Performance Improvements

## Transaction Support

Transactions ensure all operations performed inside one either all happen or none happen. This is the A, or Atomic, in ACID and therefore is critical to support. Adding support for this also brings some performance benefits around inserting, updating, and deleting multiple records at a time.

## A Refactor

Form was built in a traditional functional style with the state at the tail position of the parameter list. This was the inspiration of the "fluent" API because the function would always return the modified state and therefore would be easily piped (or method-access syntax'd) into more commands. However, after heavy usage, we've decided to swap that to be in the front. The API now reads

```fsharp
Orm.func{< Type >} OrmState Option< DbTransaction > { queryModifier(s) } { instance }
```

The state isn't really being modified and isn't the main subject in most of the API. This refactor will now allows for better piping and makes the code less noisy. Here's an example of a very simple data pipeline:
V1:
```fsharp
Orm.selectAll< ^T > state1
|> Result.map (fun data -> Orm.insertMany< ^T > false data state2)
```


V2:
```fsharp
Orm.selectAll< ^T > state1 None
|> Result.map (Orm.insertMany< ^T > state2 None false)
```

## Performance Improvements

Performance over Form 1 has been significantly improved. Our goal was to be within 30% of [Dapper](https://github.com/DapperLib/Dapper) and we're thrilled to say we are. Infact, by our benchmarking, we even surpass them!
```

BenchmarkDotNet v0.13.7, Windows 11 (10.0.22621.2134/22H2/2022Update/SunValley2)
12th Gen Intel Core i9-12900K, 1 CPU, 24 logical and 16 physical cores
.NET SDK 7.0.306
  [Host]     : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2 DEBUG
  Job-LKANEO : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2

InvocationCount=1  UnrollFactor=1

```
### Inserting
|          Method |        Data |      Mean |     Error |    StdDev |    Median | Ratio | RatioSD |      Gen0 |   Allocated | Alloc Ratio |
|---------------- |------------ |----------:|----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
|      **Form** | **Sanic[1000]** | **10.034 ms** | **0.3349 ms** | **0.9715 ms** |  **9.541 ms** |  **1.89** |    **0.14** |         **-** |  **4201.75 KB** |        **6.99** |
|  Form.InsertMany | Sanic[1000] |  5.941 ms | 0.1185 ms | 0.2771 ms |  5.817 ms |  1.05 |    0.03 |         - |    658.7 KB |        1.10 |
|    Dapper | Sanic[1000] |  6.535 ms | 0.0993 ms | 0.0929 ms |  6.525 ms |  1.11 |    0.04 |         - |  1975.55 KB |        3.29 |
| Microsoft | Sanic[1000] |  5.703 ms | 0.1114 ms | 0.2008 ms |  5.627 ms |  1.00 |    0.00 |         - |   600.69 KB |        1.00 |
|      **Form** | **Sanic[9000]** | **40.624 ms** | **0.7939 ms** | **0.8824 ms** | **40.616 ms** |  **6.97** |    **0.30** | **2000.0000** | **37795.91 KB** |       **62.92** |
|  Form.InsertMany | Sanic[9000] | 14.531 ms | 0.2257 ms | 0.3236 ms | 14.496 ms |  2.53 |    0.11 |         - |  5877.45 KB |        9.78 |
|    Dapper | Sanic[9000] | 20.316 ms | 0.3424 ms | 0.3202 ms | 20.404 ms |  3.45 |    0.11 | 1000.0000 |  17756.8 KB |       29.56 |
| Microsoft | Sanic[9000] | 13.034 ms | 0.1653 ms | 0.2524 ms | 12.959 ms |  2.27 |    0.08 |         - |  5381.94 KB |        8.96 |
### Updating
|    Method |        Data |        Mean |    Error |   StdDev | Ratio | RatioSD |      Gen0 |   Allocated | Alloc Ratio |
|---------- |------------ |------------:|---------:|---------:|------:|--------:|----------:|------------:|------------:|
|      **Form** | **Sanic[1000]** |    **16.36 ms** | **0.140 ms** | **0.131 ms** |  **1.02** |    **0.01** |   **31.2500** |   **659.23 KB** |        **1.10** |
|    Dapper | Sanic[1000] |    16.84 ms | 0.196 ms | 0.183 ms |  1.05 |    0.01 |  125.0000 |  1975.02 KB |        3.29 |
| Microsoft | Sanic[1000] |    16.02 ms | 0.120 ms | 0.107 ms |  1.00 |    0.00 |   31.2500 |   600.15 KB |        1.00 |
|      **Form** | **Sanic[9000]** | **1,197.86 ms** | **7.070 ms** | **6.613 ms** | **74.76** |    **0.63** |         **-** |  **5878.67 KB** |        **9.80** |
|    Dapper | Sanic[9000] | 1,212.24 ms | 8.144 ms | 7.220 ms | 75.66 |    0.55 | 1000.0000 | 17756.84 KB |       29.59 |
| Microsoft | Sanic[9000] | 1,203.80 ms | 7.788 ms | 7.285 ms | 75.18 |    0.70 |         - |  5381.97 KB |        8.97 |
### Selecting
|    Method |  Data |       Mean |     Error |    StdDev | Ratio | RatioSD |     Gen0 |     Gen1 |    Gen2 |  Allocated | Alloc Ratio |
|---------- |------ |-----------:|----------:|----------:|------:|--------:|---------:|---------:|--------:|-----------:|------------:|
|      **Form** |  **1000** |   **738.3 μs** |   **5.40 μs** |   **5.05 μs** |  **1.20** |    **0.01** |  **26.3672** |   **0.9766** |       **-** |  **417.94 KB** |        **2.31** |
|    Dapper |  1000 |   648.0 μs |   1.84 μs |   1.43 μs |  1.05 |    0.01 |  12.6953 |   2.9297 |       - |  197.71 KB |        1.09 |
| Microsoft |  1000 |   617.0 μs |   3.15 μs |   2.95 μs |  1.00 |    0.00 |  11.7188 |        - |       - |  180.96 KB |        1.00 |
|      **Form** | **10000** | **7,188.6 μs** | **127.72 μs** | **119.47 μs** |  **1.21** |    **0.02** | **265.6250** |        **-** |       **-** | **4144.51 KB** |        **2.30** |
|    Dapper | 10000 | 7,568.8 μs |  62.88 μs |  58.82 μs |  1.27 |    0.01 | 148.4375 | 140.6250 | 46.8750 | 2055.07 KB |        1.14 |
| Microsoft | 10000 | 5,956.2 μs |  21.63 μs |  19.17 μs |  1.00 |    0.00 | 117.1875 |        - |       - | 1798.15 KB |        1.00 |

## Wrapping up

We have achieved all this while still providing the type-safety and correctness we did in version 1. There is more to come with Form. Support for joins declared on your records is in-the-works and hopefully can be released as a patch/minor version. Stay tuned for more updates! 