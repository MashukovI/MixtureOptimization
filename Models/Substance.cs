namespace MixtureOptimization.Models
{
    public class Material
    {
        public string Name { get; set; }
        public double Cost { get; set; }
        public List<int> Parameters { get; set; } = new List<int>();
    }
    public class Substance
    {
        public string Name { get; set; }
        public int LowerLimit { get; set; }
    }
    public class Result
    {
        public string Name { get; set; }
        public double Amount { get; set; }
        public double Cost { get; set; }
    }
}
