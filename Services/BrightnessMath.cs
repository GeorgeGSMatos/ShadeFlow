namespace ShadeFlow.Services;

public static class BrightnessMath
{
    public static double Interpolate(double start, double target, double progress) =>
        Math.Clamp(start + (target - start) * Math.Clamp(progress, 0, 1), 0, 100);

    public static double Step(double current, int delta) => Math.Clamp(current + delta, 0, 100);
}
