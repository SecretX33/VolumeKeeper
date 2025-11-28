using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using VolumeKeeper.Services.Log;

namespace VolumeKeeper.Controls;

public sealed partial class DeduppedSlider : UserControl
{
    private bool _hasIgnoredFirstEvent;
    private static Logger Logger => App.Logger.Named();

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(DeduppedSlider),
        new PropertyMetadata(0.0)
    );

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(double),
        typeof(DeduppedSlider),
        new PropertyMetadata(0.0)
    );

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(DeduppedSlider),
        new PropertyMetadata(100.0)
    );

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public event RangeBaseValueChangedEventHandler? ValueChanged;

    public DeduppedSlider()
    {
        InitializeComponent();
        InternalSlider.ValueChanged += InternalSlider_ValueChanged;
    }

    private void InternalSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_hasIgnoredFirstEvent)
        {
            Logger.Debug("DeduppedSlider: Ignoring first ValueChanged event on initialization");
            _hasIgnoredFirstEvent = true;
            return; // Ignore the first event which is triggered on initialization
        }
        ValueChanged?.Invoke(this, e);
    }
}
