using Colossal.Logging;
using System;

namespace CS2M
{
    public static class Log
    {
        public static Action<string> OnErrorUI;

        public static ILog Logger { get; } = LogManager.GetLogger("CS2M")
            .SetShowsErrorsInUI(true)
            .SetEffectiveness(Level.Info)
            .SetLogStackTrace(false);

        public static void SetLoggingLevel(Level loggingLevel)
        {
            Logger.SetEffectiveness(loggingLevel);
        }

        public static void ErrorWithStackTrace(string message)
        {
            Logger.SetLogStackTrace(true);
            Logger.Error(message);
            Logger.SetLogStackTrace(false);
            OnErrorUI?.Invoke(message);
        }

        public static void Error(string message)
        {
            Logger.Error(message);
            OnErrorUI?.Invoke(message);
        }

        public static void Error(string message, Exception ex)
        {
            Logger.Error(ex, message);
            OnErrorUI?.Invoke($"{message}\n{ex.Message}");
        }

        public static void Warn(string message)
        {
            Logger.Warn(message);
        }

        public static void Info(string message)
        {
            Logger.Info(message);
        }

        public static void Debug(string message)
        {
            Logger.Debug(message);
        }

        public static void Trace(string message)
        {
            Logger.Trace(message);
        }
    }
}
