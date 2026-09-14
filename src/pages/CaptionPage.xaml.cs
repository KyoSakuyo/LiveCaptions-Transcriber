using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using LiveCaptionsTranscriber.utils;

namespace LiveCaptionsTranscriber
{
    public partial class CaptionPage : Page
    {
        public const int CARD_HEIGHT = 110;
        private const int MIN_FONT_SIZE = 8;
        private const int MAX_FONT_SIZE = 40;

        private static CaptionPage instance;
        public static CaptionPage Instance => instance;

        public CaptionPage()
        {
            InitializeComponent();
            DataContext = Transcriber.Caption;
            instance = this;
            ApplyFontSizes();

            Loaded += (s, e) =>
            {
                Transcriber.Caption.PropertyChanged += TranslatedChanged;
            };
            Unloaded += (s, e) =>
            {
                Transcriber.Caption.PropertyChanged -= TranslatedChanged;
            };
        }

        private void TextBlock_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                try
                {
                    Clipboard.SetText(textBlock.Text);
                    ShowMessage("Copied", "Transcription copied to the clipboard.");
                }
                catch (Exception ex)
                {
                    ShowMessage("Copy failed", ex.Message, true);
                }
            }
        }

        private void TranslatedChanged(object sender, PropertyChangedEventArgs e)
        {
            // Auto-scroll to bottom when new transcription is added
            if (e.PropertyName == nameof(Transcriber.Caption.FullTranscriptionText))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    TranscriptionScrollViewer.ScrollToBottom();
                }), DispatcherPriority.Background);
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(Transcriber.Caption.FullTranscriptionText))
                {
                    Clipboard.SetText(Transcriber.Caption.FullTranscriptionText);
                    ShowMessage("Copied", "Transcription copied to the clipboard.");
                }
                else
                {
                    ShowMessage("Nothing to copy", "There is no transcription yet.");
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Copy failed", ex.Message, true);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Transcriber.Caption.ClearFullTranscription();
                ShowMessage("Cleared", "The current transcription was cleared.");
            }
            catch (Exception ex)
            {
                ShowMessage("Clear failed", ex.Message, true);
            }
        }

        private static void ShowMessage(string title, string message, bool isError = false)
        {
            (Application.Current.MainWindow as MainWindow)?.ShowSnackbar(title, message, isError);
        }

        private void ApplyFontSizes()
        {
            CurrentSentence.FontSize = Transcriber.Setting.MainWindow.CurrentCaptionFontSize;
            FullTranscription.FontSize = Transcriber.Setting.MainWindow.TranscriptionFontSize;
        }

        private void CurrentSentenceCard_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;

            Transcriber.Setting.MainWindow.CurrentCaptionFontSize = AdjustFontSize(
                Transcriber.Setting.MainWindow.CurrentCaptionFontSize, e.Delta);
            ApplyFontSizes();
            e.Handled = true;
        }

        private void TranscriptionCard_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;

            Transcriber.Setting.MainWindow.TranscriptionFontSize = AdjustFontSize(
                Transcriber.Setting.MainWindow.TranscriptionFontSize, e.Delta);
            ApplyFontSizes();
            e.Handled = true;
        }

        private static int AdjustFontSize(int current, int wheelDelta)
        {
            int next = current + (wheelDelta > 0 ? 1 : -1);
            return Math.Clamp(next, MIN_FONT_SIZE, MAX_FONT_SIZE);
        }

        public void CollapseTranslatedCaption(bool isCollapsed)
        {
            // Not needed in transcription-only mode
        }


    }
}
