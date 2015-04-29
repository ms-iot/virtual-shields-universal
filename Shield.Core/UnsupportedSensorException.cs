using System;

namespace Shield
{
    public class UnsupportedSensorException : Exception
    {
        public UnsupportedSensorException()
        {
        }

        public UnsupportedSensorException(string message) : base(message)
        {
        }

        public UnsupportedSensorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}