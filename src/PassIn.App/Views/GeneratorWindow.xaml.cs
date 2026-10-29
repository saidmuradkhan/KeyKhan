using System.Windows;
using PassIn.App.ViewModels;

namespace PassIn.App.Views;

public partial class GeneratorWindow : Window
{
    public GeneratorWindow(GeneratorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => DialogResult = true;
        Closed += (_, _) => viewModel.SaveOptions();
    }
}
