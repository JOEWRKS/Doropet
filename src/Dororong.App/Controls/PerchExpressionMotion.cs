namespace Dororong.App.Controls;

internal sealed class PerchExpressionMotion
{
    private double _entry;
    private double _blink;
    internal double ScaleY
    {
        get
        {
            if(_entry<=80)return 1-.045*Smooth(_entry/80);
            if(_entry<=180)return .955+.060*Smooth((_entry-80)/100);
            return _entry<320 ? 1.015-.015*Smooth((_entry-180)/140) : 1;
        }
    }
    internal bool EyesClosed => _blink is >=2000 and <2360;
    internal void Advance(TimeSpan delta)
    {
        var ms=double.IsFinite(delta.TotalMilliseconds)?Math.Max(0,delta.TotalMilliseconds):0;
        _entry=Math.Min(320,_entry+ms);
        _blink=(_blink+ms%5000)%5000;
    }
    internal void OpenEyes()=>_blink=0;
    internal void Reset(){_entry=0;_blink=0;}
    private static double Smooth(double t)=>t*t*(3-2*t);
}
