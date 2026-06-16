using Colossal.Logging;
using LiteNetLib;

namespace CS2M
{
    /// <summary>
    ///     Implement the LiteNetLib logger interface to forward to our logger.
    /// </summary>
    public class NetLogWrapper : INetLogger
    {
        public void WriteNet(NetLogLevel level, string str, params object[] args)
        {
            switch (level)
            {
                case NetLogLevel.Info:
                    Log.Logger.DebugFormat("Network info: " + str, args);
                    break;
                case NetLogLevel.Warning:
                    Log.Logger.WarnFormat("Network warning: " + str, args);
                    break;
                case NetLogLevel.Error:
                    Log.Logger.WarnFormat("Network error: " + str, args);
                    break;
                case NetLogLevel.Trace:
                    // Ignore trace logging from LiteNetLib
                    break;
            }
        }
    }
}
