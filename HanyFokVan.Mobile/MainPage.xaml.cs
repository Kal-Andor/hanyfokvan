using HanyFokVan.Mobile.ViewModels;

namespace HanyFokVan.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MainViewModel vm)
            vm.OnAppForegrounded();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is MainViewModel vm)
            vm.OnAppBackgrounded();
    }
}
