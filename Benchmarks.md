# Benchmarks

## SqlClient + Hand-written

```

BenchmarkDotNet v0.13.6, Windows 11 (10.0.22621.1992/22H2/2022Update/SunValley2)
12th Gen Intel Core i9-12900K, 1 CPU, 24 logical and 16 physical cores
.NET SDK 7.0.306
  [Host]     : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2 DEBUG
  DefaultJob : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2


```
|      Method |        Mean |     Error |    StdDev |    Gen0 | Allocated |
|------------ |------------:|----------:|----------:|--------:|----------:|
| InsertSmall | 5,691.56 μs | 57.415 μs | 50.897 μs | 31.2500 | 596.04 KB |
| UpdateSmall |   935.77 μs |  7.005 μs |  6.210 μs | 38.0859 | 596.06 KB |
| SelectSmall |    22.28 μs |  0.152 μs |  0.142 μs |  0.0610 |   1.16 KB |

## Dapper

``` 

BenchmarkDotNet v0.13.6, Windows 11 (10.0.22621.1992/22H2/2022Update/SunValley2)
12th Gen Intel Core i9-12900K, 1 CPU, 24 logical and 16 physical cores
.NET SDK 7.0.306
  [Host]     : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2 DEBUG
  DefaultJob : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2


```
|      Method |        Mean |     Error |    StdDev |     Gen0 |  Allocated |
|------------ |------------:|----------:|----------:|---------:|-----------:|
| InsertSmall | 6,162.72 μs | 49.654 μs | 41.463 μs | 101.5625 | 1603.79 KB |
| UpdateSmall | 1,466.81 μs |  9.954 μs |  9.311 μs | 103.5156 | 1603.81 KB |
| SelectSmall |    23.63 μs |  0.168 μs |  0.149 μs |   0.1526 |    2.46 KB |

## Form 

```

BenchmarkDotNet v0.13.6, Windows 11 (10.0.22621.1992/22H2/2022Update/SunValley2)
12th Gen Intel Core i9-12900K, 1 CPU, 24 logical and 16 physical cores
.NET SDK 7.0.306
  [Host]     : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2 DEBUG
  DefaultJob : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2


```
|      Method |         Mean |      Error |     StdDev |      Gen0 |     Gen1 |   Allocated |
|------------ |-------------:|-----------:|-----------:|----------:|---------:|------------:|
| InsertSmall | 29,594.97 μs | 382.307 μs | 319.244 μs | 1250.0000 |  62.5000 | 19456.86 KB |
| UpdateSmall | 27,124.58 μs | 241.490 μs | 225.890 μs | 1406.2500 | 187.5000 | 21776.49 KB |
| SelectSmall |     58.01 μs |   0.751 μs |   0.702 μs |    1.5869 |   0.4883 |    24.66 KB |


