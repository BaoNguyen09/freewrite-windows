using System.Windows;

using System.Windows.Controls;

using System.Windows.Media;

using System.Windows.Shapes;



namespace FreewriteWindows;



public sealed class WaveformBar : Panel

{

    public const int BarCount = 96;

    private const float SilenceLevel = 0.012f;

    private const double MinBarHeight = 2;

    private const double MaxBarHeight = 22;



    private readonly Rectangle[] _bars = new Rectangle[BarCount];

    private readonly Queue<float> _history = new(BarCount);



    public static readonly DependencyProperty BarBrushProperty =

        DependencyProperty.Register(nameof(BarBrush), typeof(Brush), typeof(WaveformBar),

            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(170, 170, 170))));



    public Brush BarBrush

    {

        get => (Brush)GetValue(BarBrushProperty);

        set => SetValue(BarBrushProperty, value);

    }



    public WaveformBar()

    {

        for (var i = 0; i < BarCount; i++)

        {

            _history.Enqueue(SilenceLevel);

        }



        for (var i = 0; i < BarCount; i++)

        {

            var bar = new Rectangle

            {

                RadiusX = 1,

                RadiusY = 1,

                VerticalAlignment = VerticalAlignment.Center,

                Fill = BarBrush

            };

            _bars[i] = bar;

            Children.Add(bar);

        }

    }



    public void PushLevel(float level)

    {

        var boosted = Math.Clamp(level, 0f, 1f);

        _history.Dequeue();

        _history.Enqueue(boosted);

        UpdateBars();

        InvalidateArrange();

    }



    public void Reset()

    {

        _history.Clear();

        for (var i = 0; i < BarCount; i++)

        {

            _history.Enqueue(SilenceLevel);

        }



        UpdateBars();

        InvalidateArrange();

    }



    private void UpdateBars()

    {

        var values = _history.ToArray();

        for (var i = 0; i < BarCount; i++)

        {

            _bars[i].Height = HeightForLevel(values[i]);

            _bars[i].Fill = BarBrush;

        }

    }



    private static double HeightForLevel(float level)

    {

        if (level < 0.07f)

        {

            return MinBarHeight;

        }



        var scaled = (level - 0.07f) / 0.93f;

        return MinBarHeight + Math.Pow(scaled, 0.72) * (MaxBarHeight - MinBarHeight);

    }



    protected override Size MeasureOverride(Size availableSize)

    {

        var width = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0

            ? 320

            : availableSize.Width;

        return new Size(width, 24);

    }



    protected override Size ArrangeOverride(Size finalSize)

    {

        var pitch = finalSize.Width / BarCount;

        var barWidth = Math.Clamp(pitch * 0.28, 1.5, 2.5);

        var radius = barWidth / 2;



        for (var i = 0; i < BarCount; i++)

        {

            var bar = _bars[i];

            var height = bar.Height;

            var x = (i * pitch) + ((pitch - barWidth) / 2);

            bar.Width = barWidth;

            bar.RadiusX = radius;

            bar.RadiusY = radius;

            bar.Arrange(new Rect(x, (finalSize.Height - height) / 2, barWidth, height));

        }



        return finalSize;

    }

}


