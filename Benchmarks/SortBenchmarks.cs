using BenchmarkDotNet.Attributes;

[MemoryDiagnoser] // Tracks RAM allocations
public class SortBenchmarks
{
    private const int N = 20;
    private const int Loops = 1_000_000; // Increase work per iteration
    private int[][] _originalData;
    private int[][] _workData;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        // Create 1000 different small arrays to avoid "lucky" branch prediction
        _originalData = Enumerable.Range(0, Loops)
            .Select(_ => Enumerable.Range(0, N).Select(__ => random.Next(0, 1000)).ToArray())
            .ToArray();

        _workData = new int[Loops][];
        for (int i = 0; i < Loops; i++) _workData[i] = new int[N];
    }

    [IterationSetup]
    public void IterationSetup()
    {
        for (int i = 0; i < Loops; i++)
            Array.Copy(_originalData[i], _workData[i], N);
    }

    [Benchmark(OperationsPerInvoke = Loops)]
    public void ArraySort()
    {
        for (int k = 0; k < Loops; k++)
            Array.Sort(_workData[k]);
    }

    [Benchmark(OperationsPerInvoke = Loops)]
    public void SpanSort()
    {
        for (int k = 0; k < Loops; k++)
        {
            _workData[k].AsSpan().Sort();
        }
    }

    [Benchmark(OperationsPerInvoke = Loops)]
    public void ManualInsertionSort()
    {
        for (int k = 0; k < Loops; k++)
        {
            Span<int> span = _workData[k];
            for (int i = 1; i < span.Length; i++)
            {
                int key = span[i];
                int j = i - 1;
                while (j >= 0 && span[j] > key)
                {
                    span[j + 1] = span[j];
                    j--;
                }
                span[j + 1] = key;
            }
        }
    }

    [Benchmark(OperationsPerInvoke = Loops)]
    public void LinqOrderBy()
    {
        // This is usually the slowest due to allocations and overhead
        for (int k = 0; k < Loops; k++)
        {
            var sorted = _workData[k].OrderBy(x => x).ToArray();
        }
    }
}

