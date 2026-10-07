using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WorkDelta.App.Localization;
using Image = System.Windows.Controls.Image;
using MessageBox = System.Windows.MessageBox;

namespace WorkDelta.App;

public partial class SupportWindow : Window
{
    private const string PayPalUrl = "https://www.paypal.com/ncp/payment/SC8FJDJTVTBD2";

    public SupportWindow() => InitializeComponent();

    private void OpenPayPal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(PayPalUrl) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"{Localizer.Get("PaymentOpenFailed")}\n\n{exception.Message}",
                Localizer.Get("AppName"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PreviewImage_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Image source || source.Source is not BitmapSource bitmap)
        {
            return;
        }

        var preview = new Window
        {
            Owner = this,
            Title = Localizer.Get("QrPreview"),
            Width = 700,
            Height = 800,
            MinWidth = 420,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = System.Windows.Media.Brushes.White,
            Content = new ScrollViewer
            {
                Margin = new Thickness(18),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new Image { Source = bitmap, Stretch = System.Windows.Media.Stretch.Uniform }
            }
        };
        preview.ShowDialog();
    }
}
