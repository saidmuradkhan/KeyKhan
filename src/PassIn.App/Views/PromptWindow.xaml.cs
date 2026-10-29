using System.Windows;

namespace PassIn.App.Views;

/// <summary>Asks for a single line of text.</summary>
public partial class PromptWindow : Window
{
    public PromptWindow(string title, string label, string initialValue)
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        Input.Text = initialValue;
        Loaded += (_, _) =>
        {
            Input.Focus();
            Input.SelectAll();
        };
    }

    public string Value => Input.Text.Trim();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (Value.Length > 0)
        {
            DialogResult = true;
        }
    }
}
