namespace Yoji
{
    [System.Flags]
    public enum LineFlag
    {
        NoCull = (1<<0),
        NoSmoothAngle = (1<<1),
        NoFront = (1<<2),
    }
}
