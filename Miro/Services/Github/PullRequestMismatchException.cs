using System;
using System.Runtime.Serialization;

namespace Miro.Services.Github
{
    [Serializable]
    internal class PullRequestMismatchException : Exception
    {
        private object httpResponse;

        public PullRequestMismatchException()
        {
        }

        public PullRequestMismatchException(object httpResponse)
        {
            this.httpResponse = httpResponse;
        }

        public PullRequestMismatchException(string message) : base(message)
        {
        }

        public PullRequestMismatchException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected PullRequestMismatchException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}