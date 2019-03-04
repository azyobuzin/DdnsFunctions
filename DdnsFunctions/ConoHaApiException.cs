using System;

namespace DdnsFunctions
{
    public class ConoHaApiException : Exception
    {
        public ConoHaApiException(string message) : base(message) { }
    }
}
