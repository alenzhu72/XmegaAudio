using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace XmegaAudio.Controls;

public sealed class EqCurveControl : FrameworkElement
{
    private static readonly float[] BandFrequenciesHz = new[]
    {
        31.25f, 62.5f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f
    };
    private readonly float[] gainsDb = new float[BandFrequenciesHz.Length];
    private int? draggingIndex;

    public event EventHandler<float[]>? BandGainsChanged;

    public float MaxGainDb { get; set; } = 12f;

    public void SetBandGains(float[] bandGainsDb)
    {
        int len = Math.Min(gainsDb.Length, bandGainsDb.Length);
        Array.Copy(bandGainsDb, gainsDb, len);
        InvalidateVisual();
    }

    public float[] GetBandGainsCopy()
    {
        var copy = new float[gainsDb.Length];
        Array.Copy(gainsDb, copy, gainsDb.Length);
        return copy;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(18, 18, 20)), new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 66)), 1), rect);

        if (ActualWidth <= 1 || ActualHeight <= 1)
        {
            return;
        }

        DrawGrid(dc, rect);
        DrawCurve(dc, rect);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        var p = e.GetPosition(this);
        int? idx = FindNearestPointIndex(p);
        if (idx is null)
        {
            return;
        }

        draggingIndex = idx;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (draggingIndex is null || !IsMouseCaptured)
        {
            return;
        }

        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        var p = e.GetPosition(this);

        float gain = YToGainDb(p.Y, rect);
        gainsDb[draggingIndex.Value] = gain;

        BandGainsChanged?.Invoke(this, GetBandGainsCopy());
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (!IsMouseCaptured)
        {
            return;
        }

        draggingIndex = null;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void DrawGrid(DrawingContext dc, Rect rect)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 46)), 1);
        gridPen.Freeze();

        float maxGain = Math.Max(1f, MaxGainDb);
        for (int i = -3; i <= 3; i++)
        {
            float g = i * (maxGain / 3f);
            double y = GainDbToY(g, rect);
            dc.DrawLine(gridPen, new Point(0, y), new Point(rect.Width, y));
        }

        float[] freqs = new[] { 20f, 50f, 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f, 20000f };
        for (int i = 0; i < freqs.Length; i++)
        {
            double x = FreqToX(freqs[i], rect);
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x, rect.Height));
        }
    }

    private void DrawCurve(DrawingContext dc, Rect rect)
    {
        var linePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 200, 255)), 2);
        linePen.Freeze();

        var pointBrush = new SolidColorBrush(Color.FromRgb(230, 230, 235));
        pointBrush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (int i = 0; i < BandFrequenciesHz.Length; i++)
            {
                double x = FreqToX(BandFrequenciesHz[i], rect);
                double y = GainDbToY(gainsDb[i], rect);
                if (i == 0)
                {
                    ctx.BeginFigure(new Point(x, y), false, false);
                }
                else
                {
                    ctx.LineTo(new Point(x, y), true, false);
                }
            }
        }

        geo.Freeze();
        dc.DrawGeometry(null, linePen, geo);

        for (int i = 0; i < BandFrequenciesHz.Length; i++)
        {
            double x = FreqToX(BandFrequenciesHz[i], rect);
            double y = GainDbToY(gainsDb[i], rect);
            dc.DrawEllipse(pointBrush, null, new Point(x, y), 5, 5);
        }
    }

    private int? FindNearestPointIndex(Point p)
    {
        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        double best = double.MaxValue;
        int? bestIndex = null;

        for (int i = 0; i < BandFrequenciesHz.Length; i++)
        {
            double x = FreqToX(BandFrequenciesHz[i], rect);
            double y = GainDbToY(gainsDb[i], rect);
            double dx = p.X - x;
            double dy = p.Y - y;
            double d2 = dx * dx + dy * dy;
            if (d2 < best)
            {
                best = d2;
                bestIndex = i;
            }
        }

        return best <= 15 * 15 ? bestIndex : null;
    }

    private static double FreqToX(float freqHz, Rect rect)
    {
        const float minHz = 20f;
        const float maxHz = 20000f;
        double minL = Math.Log10(minHz);
        double maxL = Math.Log10(maxHz);
        double v = (Math.Log10(Math.Clamp(freqHz, minHz, maxHz)) - minL) / (maxL - minL);
        return rect.Left + v * rect.Width;
    }

    private double GainDbToY(float gainDb, Rect rect)
    {
        float maxGain = Math.Max(1f, MaxGainDb);
        float g = Math.Clamp(gainDb, -maxGain, maxGain);
        double v = (g + maxGain) / (2f * maxGain);
        return rect.Top + (1.0 - v) * rect.Height;
    }

    private float YToGainDb(double y, Rect rect)
    {
        float maxGain = Math.Max(1f, MaxGainDb);
        double v = 1.0 - (Math.Clamp(y, rect.Top, rect.Bottom) - rect.Top) / rect.Height;
        return (float)(v * 2.0 * maxGain - maxGain);
    }
}
