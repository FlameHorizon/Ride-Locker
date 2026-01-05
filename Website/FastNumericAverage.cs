namespace Website;

public class FastNumericAverageDouble
{
    private readonly List<double> _buffer = [];
    private double _sum = 0.0;
    private int _count = 0;

    public double Average => _sum / _count;

    public void Add(double item)
    {
        _sum += item;
        _count++;
    }
}