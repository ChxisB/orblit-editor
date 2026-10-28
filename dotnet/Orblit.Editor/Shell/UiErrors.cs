using Avalonia.Controls;
using Avalonia.Threading;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>What the UI thread throws, sent to the console rather than to a terminal nobody is watching.</summary>
internal static class UiErrors
{
    /// <summary>
    /// Catches into <paramref name="log"/> until <paramref name="window"/> closes. A handler that outlives
    /// the editor it writes into is how a closed editor keeps reporting.
    /// </summary>
    /// <remarks>
    /// Marked handled, so the editor carries on as the Dart one does after a widget throws, and what is
    /// open can still be saved.
    /// </remarks>
    public static void ToLog(EditorLog log, Window window)
    {
        void Catch(object? sender, DispatcherUnhandledExceptionEventArgs thrown)
        {
            log.Say(thrown.Exception.Message, SayLevel.Error, thrown.Exception.ToString());
            thrown.Handled = true;
        }

        Dispatcher.UIThread.UnhandledException += Catch;
        window.Closed += (_, _) => Dispatcher.UIThread.UnhandledException -= Catch;
    }
}
