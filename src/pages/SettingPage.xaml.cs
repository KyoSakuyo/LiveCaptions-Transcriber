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
        private bool isUpdatingMicrophoneAudio;

        public SettingPage()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            Loaded += SettingPage_Loaded;
        }

        private async void SettingPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshLiveCaptionsButton();
            await RefreshMicrophoneAudioStateAsync();
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

        private async Task RefreshMicrophoneAudioStateAsync()
        {
            AutomationElement? window = Transcriber.Window;
            if (window == null)
            {
                SetMicrophoneAudioUnavailable(
                    "Windows Live Captions is restarting. Open Settings again in a moment.");
                return;
            }

            isUpdatingMicrophoneAudio = true;
            MicrophoneAudioToggle.IsEnabled = false;
            MicrophoneAudioStatus.Text = "Checking the current Windows setting…";

            MicrophoneAudioPreferenceResult result =
                await LiveCaptionsHandler.GetMicrophoneAudioEnabledAsync(window);
            if (result.Success && result.IsEnabled.HasValue)
            {
                MicrophoneAudioToggle.IsChecked = result.IsEnabled.Value;
                MicrophoneAudioToggle.IsEnabled = true;
                MicrophoneAudioStatus.Text = result.IsEnabled.Value
                    ? "Microphone audio is included in Windows Live Captions."
                    : "Microphone audio is currently excluded.";
            }
            else
            {
                SetMicrophoneAudioUnavailable(result.ErrorMessage);
            }
            isUpdatingMicrophoneAudio = false;
        }

        private async void MicrophoneAudioToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingMicrophoneAudio || !MicrophoneAudioToggle.IsChecked.HasValue)
                return;

            AutomationElement? window = Transcriber.Window;
            if (window == null)
            {
                SetMicrophoneAudioUnavailable(
                    "Windows Live Captions is restarting. Open Settings again in a moment.");
                return;
            }

            bool desiredState = MicrophoneAudioToggle.IsChecked.Value;
            isUpdatingMicrophoneAudio = true;
            MicrophoneAudioToggle.IsEnabled = false;
            MicrophoneAudioStatus.Text = desiredState
                ? "Enabling microphone audio…"
                : "Disabling microphone audio…";

            MicrophoneAudioPreferenceResult result =
                await LiveCaptionsHandler.SetMicrophoneAudioEnabledAsync(window, desiredState);
            if (result.Success && result.IsEnabled.HasValue)
            {
                MicrophoneAudioToggle.IsChecked = result.IsEnabled.Value;
                MicrophoneAudioToggle.IsEnabled = true;
                MicrophoneAudioStatus.Text = result.IsEnabled.Value
                    ? "Microphone audio is included in Windows Live Captions."
                    : "Microphone audio is currently excluded.";
                (Application.Current.MainWindow as MainWindow)?.ShowSnackbar(
                    "Live Captions updated",
                    result.IsEnabled.Value
                        ? "Microphone audio is now included."
                        : "Microphone audio is now excluded.");
            }
            else
            {
                SetMicrophoneAudioUnavailable(result.ErrorMessage);
                ShowError(result.ErrorMessage);
            }
            isUpdatingMicrophoneAudio = false;
        }

        private void SetMicrophoneAudioUnavailable(string message)
        {
            MicrophoneAudioToggle.IsChecked = null;
            MicrophoneAudioToggle.IsEnabled = false;
            MicrophoneAudioStatus.Text = message;
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
