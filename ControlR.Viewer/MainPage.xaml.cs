namespace ControlR.Viewer;

public partial class MainPage : ContentPage
{
    private static MainPage? _default;

    public MainPage()
    {
        InitializeComponent();
        _default = this;
    }

    public static MainPage Current
    {
        get
        {
            return _default ??= new MainPage();
        }
    }
}