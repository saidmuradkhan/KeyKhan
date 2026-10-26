using System.Windows;
using System.Windows.Controls;

namespace KeyKhan.App.Controls;

/// <summary>A password field with a reveal toggle that supports two-way binding. Template lives in Styles.xaml.</summary>
public sealed class SecretBox : Control
{
    public static readonly DependencyProperty SecretProperty = DependencyProperty.Register(
        nameof(Secret), typeof(string), typeof(SecretBox),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSecretChanged));

    public static readonly DependencyProperty IsRevealedProperty = DependencyProperty.Register(
        nameof(IsRevealed), typeof(bool), typeof(SecretBox), new PropertyMetadata(false));

    private PasswordBox? _passwordBox;
    private bool _syncing;

    public string Secret
    {
        get => (string)GetValue(SecretProperty);
        set => SetValue(SecretProperty, value);
    }

    public bool IsRevealed
    {
        get => (bool)GetValue(IsRevealedProperty);
        set => SetValue(IsRevealedProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_passwordBox is not null)
        {
            _passwordBox.PasswordChanged -= OnPasswordChanged;
        }

        _passwordBox = GetTemplateChild("PART_PasswordBox") as PasswordBox;
        if (_passwordBox is not null)
        {
            _passwordBox.Password = Secret;
            _passwordBox.PasswordChanged += OnPasswordChanged;
        }
    }

    private static void OnSecretChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (SecretBox)d;
        if (!box._syncing && box._passwordBox is not null && box._passwordBox.Password != (string)e.NewValue)
        {
            box._syncing = true;
            box._passwordBox.Password = (string)e.NewValue;
            box._syncing = false;
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        Secret = _passwordBox!.Password;
        _syncing = false;
    }
}
