using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VolumeKeeper.Controls;

public sealed partial class CompactToggleSwitch : UserControl
{
    private int _clickedCount;

    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn),
        typeof(bool),
        typeof(CompactToggleSwitch),
        new PropertyMetadata(false, OnIsOnChanged)
    );

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(object),
        typeof(CompactToggleSwitch),
        new PropertyMetadata(null, OnHeaderChanged)
    );

    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public event RoutedEventHandler? Toggled;

    public CompactToggleSwitch()
    {
        InitializeComponent();
        InternalToggle.Toggled += InternalToggle_Toggled;
    }

    private static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CompactToggleSwitch control)
        {
            control.InternalToggle.IsOn = (bool)e.NewValue;
        }
    }

    private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CompactToggleSwitch control)
        {
            control.InternalToggle.Header = e.NewValue;
        }
    }

    private void InternalToggle_Toggled(object sender, RoutedEventArgs e)
    {
        IsOn = InternalToggle.IsOn;
        if (_clickedCount++ == 0) return; // Ignore the first event which is triggered on initialization
        Toggled?.Invoke(this, e);
    }
}
