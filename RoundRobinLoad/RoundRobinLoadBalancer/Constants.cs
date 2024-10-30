namespace RoundRobinLoadBalancer
{
    public static class Constants
    {
        //Constants required for Server Weight Calculation based on Dynamic Ratio F5 LTM
        public const double cpuCoeff = 1.5;
        public const double cpuThreshold = 80;
        public const double diskCoeff = 2.0;
        public const double diskThreshold = 90;
        public const double memoryCoeff = 1.0;
        public const double memoryThreshold = 70;
    }
}
