using HanyFokVan.Mobile.ViewModels;

namespace HanyFokVan.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
