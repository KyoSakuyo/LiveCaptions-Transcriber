using System.Diagnostics;
using System.Windows.Automation;

namespace LiveCaptionsTranscriber.utils
{
    public static class LiveCaptionsHandler
    {
        public static readonly string PROCESS_NAME = "LiveCaptions";

        private static readonly string[] SettingsNames =
            ["Settings", "设置", "設定", "Paramètres"];
        private static readonly string[] PreferencesNames =
            ["Preferences", "首选项", "偏好设置", "偏好設定", "喜好設定", "Préférences"];
        private static readonly string[] MicrophoneAudioNames =
        [
            "Include microphone audio",
            "包括麦克风音频",
            "包含麦克风音频",
            "包括麥克風音訊",
            "包含麥克風音訊",
            "Inclure l’audio du microphone"
        ];

        private static AutomationElement? captionsTextBlock = null;

        public static AutomationElement LaunchLiveCaptions()
        {
            // Init
            KillAllProcessesByPName(PROCESS_NAME);
            var process = Process.Start(PROCESS_NAME);

            // Search for window
            AutomationElement? window = null;
            for (int attemptCount = 0;
                 window == null || window.Current.ClassName.CompareTo("LiveCaptionsDesktopWindow") != 0;
                 attemptCount++)
            {
                window = FindWindowByPId(process.Id);
                if (attemptCount > 10000)
                    throw new Exception("Failed to launch LiveCaptions!");
            }

            return window;
        }

        public static void KillLiveCaptions(AutomationElement window)
        {
            // Search for process
            nint hWnd = new nint((long)window.Current.NativeWindowHandle);
            WindowsAPI.GetWindowThreadProcessId(hWnd, out int processId);
            var process = Process.GetProcessById(processId);

            // Kill process
            process.Kill();
            process.WaitForExit();
        }

        public static void HideLiveCaptions(AutomationElement window)
        {
            nint hWnd = new nint((long)window.Current.NativeWindowHandle);
            int exStyle = WindowsAPI.GetWindowLong(hWnd, WindowsAPI.GWL_EXSTYLE);

            WindowsAPI.ShowWindow(hWnd, WindowsAPI.SW_MINIMIZE);
            WindowsAPI.SetWindowLong(hWnd, WindowsAPI.GWL_EXSTYLE, exStyle | WindowsAPI.WS_EX_TOOLWINDOW);
        }

        public static void RestoreLiveCaptions(AutomationElement window)
        {
            nint hWnd = new nint((long)window.Current.NativeWindowHandle);
            int exStyle = WindowsAPI.GetWindowLong(hWnd, WindowsAPI.GWL_EXSTYLE);

            WindowsAPI.SetWindowLong(hWnd, WindowsAPI.GWL_EXSTYLE, exStyle & ~WindowsAPI.WS_EX_TOOLWINDOW);
            WindowsAPI.ShowWindow(hWnd, WindowsAPI.SW_RESTORE);
            WindowsAPI.SetForegroundWindow(hWnd);
        }

        public static Task<MicrophoneAudioPreferenceResult> GetMicrophoneAudioEnabledAsync(
            AutomationElement window, CancellationToken token = default)
        {
            return AccessMicrophoneAudioPreferenceAsync(window, null, token);
        }

        public static Task<MicrophoneAudioPreferenceResult> SetMicrophoneAudioEnabledAsync(
            AutomationElement window, bool enabled, CancellationToken token = default)
        {
            return AccessMicrophoneAudioPreferenceAsync(window, enabled, token);
        }

        private static async Task<MicrophoneAudioPreferenceResult> AccessMicrophoneAudioPreferenceAsync(
            AutomationElement window, bool? desiredState, CancellationToken token)
        {
            bool wasHidden = false;
            int? processId = null;
            AutomationElement? settingsButton = null;
            try
            {
                wasHidden = window.Current.BoundingRectangle == System.Windows.Rect.Empty;
                if (wasHidden)
                    RestoreLiveCaptions(window);

                token.ThrowIfCancellationRequested();
                processId = window.Current.ProcessId;
                settingsButton =
                    FindElementByAId(window, "SettingsButton", token) ??
                    FindElementByNames(window, SettingsNames, token);
                if (settingsButton == null || !TryActivate(settingsButton))
                    return MicrophoneAudioPreferenceResult.Failed(
                        "The Windows Live Captions Settings button could not be opened.");

                AutomationElement? preferencesItem = await WaitForElementByNamesAsync(
                    processId.Value, PreferencesNames, token);
                if (preferencesItem == null || !TryActivate(preferencesItem))
                    return MicrophoneAudioPreferenceResult.Failed(
                        "The Windows Live Captions Preferences menu could not be opened.");

                AutomationElement? microphoneItem = await WaitForElementByNamesAsync(
                    processId.Value, MicrophoneAudioNames, token);
                if (microphoneItem == null)
                    return MicrophoneAudioPreferenceResult.Failed(
                        "The Include microphone audio option is unavailable on this Windows version or display language.");

                bool? currentState = GetToggleState(microphoneItem);
                if (desiredState is bool desired)
                {
                    if (!currentState.HasValue)
                        return MicrophoneAudioPreferenceResult.Failed(
                            "Windows Live Captions did not expose the current microphone audio state.");

                    if (currentState.Value != desired)
                    {
                        if (!TryToggle(microphoneItem))
                            return MicrophoneAudioPreferenceResult.Failed(
                                "The Include microphone audio option could not be changed.");

                        await Task.Delay(150, token);
                        currentState = GetToggleState(microphoneItem) ?? desired;
                    }
                }

                if (!currentState.HasValue)
                    return MicrophoneAudioPreferenceResult.Failed(
                        "Windows Live Captions did not expose the current microphone audio state.");

                return MicrophoneAudioPreferenceResult.Succeeded(currentState.Value);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ElementNotAvailableException)
            {
                return MicrophoneAudioPreferenceResult.Failed(
                    "Windows Live Captions restarted while reading its settings.");
            }
            catch (Exception ex)
            {
                return MicrophoneAudioPreferenceResult.Failed(ex.Message);
            }
            finally
            {
                try
                {
                    if (wasHidden)
                        HideLiveCaptions(window);
                    else if (processId.HasValue && settingsButton != null &&
                             FindElementByNames(
                                 AutomationElement.RootElement,
                                 PreferencesNames,
                                 CancellationToken.None,
                                 processId.Value) != null)
                        TryActivate(settingsButton);
                }
                catch (Exception)
                {
                    // Preserve the preference result if the captions window closes during cleanup.
                }
            }
        }

        private static async Task<AutomationElement?> WaitForElementByNamesAsync(
            int processId, IReadOnlyCollection<string> names, CancellationToken token)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                token.ThrowIfCancellationRequested();
                AutomationElement? element = FindElementByNames(
                    AutomationElement.RootElement, names, token, processId);
                if (element != null)
                    return element;
                await Task.Delay(75, token);
            }
            return null;
        }

        private static AutomationElement? FindElementByNames(
            AutomationElement scope,
            IReadOnlyCollection<string> names,
            CancellationToken token,
            int? processId = null)
        {
            Condition condition = processId.HasValue
                ? new PropertyCondition(AutomationElement.ProcessIdProperty, processId.Value)
                : Condition.TrueCondition;
            AutomationElementCollection elements = scope.FindAll(TreeScope.Descendants, condition);

            foreach (AutomationElement element in elements)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    string name = NormalizeName(element.Current.Name);
                    if (names.Any(expected => string.Equals(
                        name, NormalizeName(expected), StringComparison.OrdinalIgnoreCase)))
                        return element;
                }
                catch (ElementNotAvailableException)
                {
                    continue;
                }
            }
            return null;
        }

        private static string NormalizeName(string? value)
        {
            return string.Join(" ", (value ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool TryActivate(AutomationElement element)
        {
            try
            {
                if (element.TryGetCurrentPattern(InvokePattern.Pattern, out object invoke))
                {
                    ((InvokePattern)invoke).Invoke();
                    return true;
                }
                if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object selection))
                {
                    ((SelectionItemPattern)selection).Select();
                    return true;
                }
                if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object expand))
                {
                    ((ExpandCollapsePattern)expand).Expand();
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            return false;
        }

        private static bool TryToggle(AutomationElement element)
        {
            try
            {
                if (element.TryGetCurrentPattern(TogglePattern.Pattern, out object toggle))
                {
                    ((TogglePattern)toggle).Toggle();
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            return TryActivate(element);
        }

        private static bool? GetToggleState(AutomationElement element)
        {
            try
            {
                if (element.TryGetCurrentPattern(TogglePattern.Pattern, out object toggle))
                {
                    ToggleState state = ((TogglePattern)toggle).Current.ToggleState;
                    return state == ToggleState.On ? true :
                        state == ToggleState.Off ? false : null;
                }
                if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object selection))
                    return ((SelectionItemPattern)selection).Current.IsSelected;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            return null;
        }

        public static void FixLiveCaptions(AutomationElement window)
        {
            nint hWnd = new nint((long)window.Current.NativeWindowHandle);

            RECT rect;
            if (!WindowsAPI.GetWindowRect(hWnd, out rect))
                throw new Exception("Unable to get the window rectangle of LiveCaptions!");
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            int x = rect.Left;
            int y = rect.Top;

            bool isSuccess = true;
            if (x < 0 || y < 0 || width < 100 || height < 100)
                isSuccess = WindowsAPI.MoveWindow(hWnd, 800, 600, 600, 200, true);
            if (!isSuccess)
                throw new Exception("Failed to fix LiveCaptions!");
        }

        public static string GetCaptions(AutomationElement window)
        {
            if (captionsTextBlock == null)
                captionsTextBlock = FindElementByAId(window, "CaptionsTextBlock");
            try
            {
                return captionsTextBlock?.Current.Name ?? string.Empty;
            }
            catch (ElementNotAvailableException)
            {
                captionsTextBlock = null;
                throw;
            }
        }

        private static AutomationElement FindWindowByPId(int processId)
        {
            var condition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
            return AutomationElement.RootElement.FindFirst(TreeScope.Children, condition);
        }

        public static AutomationElement? FindElementByAId(
            AutomationElement window, string automationId, CancellationToken token = default)
        {
            try
            {
                PropertyCondition condition = new PropertyCondition(
                    AutomationElement.AutomationIdProperty, automationId);
                return window.FindFirst(TreeScope.Descendants, condition);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        public static void PrintAllElementsAId(AutomationElement window)
        {
            var treeWalker = TreeWalker.RawViewWalker;
            var stack = new Stack<AutomationElement>();
            stack.Push(window);

            while (stack.Count > 0)
            {
                var element = stack.Pop();
                if (!string.IsNullOrEmpty(element.Current.AutomationId))
                    Console.WriteLine(element.Current.AutomationId);

                var child = treeWalker.GetFirstChild(element);
                while (child != null)
                {
                    stack.Push(child);
                    child = treeWalker.GetNextSibling(child);
                }
            }
        }

        public static bool ClickSettingsButton(AutomationElement window)
        {
            var settingsButton = FindElementByAId(window, "SettingsButton");
            if (settingsButton != null)
            {
                var invokePattern = settingsButton.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if (invokePattern != null)
                {
                    invokePattern.Invoke();
                    return true;
                }
            }
            return false;
        }

        private static void KillAllProcessesByPName(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
                return;
            foreach (Process process in processes)
            {
                process.Kill();
                process.WaitForExit();
            }
        }
    }

    public sealed class MicrophoneAudioPreferenceResult
    {
        public bool Success { get; }
        public bool? IsEnabled { get; }
        public string ErrorMessage { get; }

        private MicrophoneAudioPreferenceResult(bool success, bool? isEnabled, string errorMessage)
        {
            Success = success;
            IsEnabled = isEnabled;
            ErrorMessage = errorMessage;
        }

        public static MicrophoneAudioPreferenceResult Succeeded(bool isEnabled)
        {
            return new MicrophoneAudioPreferenceResult(true, isEnabled, string.Empty);
        }

        public static MicrophoneAudioPreferenceResult Failed(string errorMessage)
        {
            return new MicrophoneAudioPreferenceResult(false, null, errorMessage);
        }
    }
}
