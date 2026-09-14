using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Appearance;

using LiveCaptionsTranscriber.utils;

namespace LiveCaptionsTranscriber
{
    public partial class SettingPage : Page
    {
        public SettingPage()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            Loaded += (_, _) => RefreshLiveCaptionsButton();
        }

        private void LiveCaptionsButton_Click(object sender, RoutedEventArgs e)
        {
            AutomationElement? window = Transcriber.Window;
            if (window == null)
            {
                ShowError("Windows Live Captions is restarting. Please try again in a moment.");
                return;
            }

            try
            {
                if (IsLiveCaptionsHidden(window))
                    LiveCaptionsHandler.RestoreLiveCaptions(window);
                else
                    LiveCaptionsHandler.HideLiveCaptions(window);

                RefreshLiveCaptionsButton();
            }
            catch (ElementNotAvailableException)
            {
                ShowError("Windows Live Captions is restarting. Please try again in a moment.");
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void RefreshLiveCaptionsButton()
        {
            try
            {
                LiveCaptionsButton.IsEnabled = Transcriber.Window != null;
                LiveCaptionsButton.Content = Transcriber.Window == null ||
                    IsLiveCaptionsHidden(Transcriber.Window) ? "Show" : "Hide";
            }
            catch (ElementNotAvailableException)
            {
                LiveCaptionsButton.IsEnabled = false;
                LiveCaptionsButton.Content = "Show";
            }
        }

        private static bool IsLiveCaptionsHidden(AutomationElement window)
        {
            return window.Current.BoundingRectangle == Rect.Empty;
        }

        private static void ShowError(string message)
        {
            (Application.Current.MainWindow as MainWindow)?.ShowSnackbar(
                "Live Captions unavailable", message, true);
        }

        private void LiveCaptionsInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Show();
        }

        private void LiveCaptionsInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Hide();
        }
    }
}
