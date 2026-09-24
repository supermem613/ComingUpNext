namespace ComingUpNextTray.Services
{
    /// <summary>Reports an actionable failure while resolving or invoking Work IQ.</summary>
    internal sealed class WorkIqException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="WorkIqException"/> class.</summary>
        public WorkIqException()
            : base("Work IQ operation failed.")
        {
        }

        /// <summary>Initializes a new instance of the <see cref="WorkIqException"/> class.</summary>
        /// <param name="message">Actionable error message.</param>
        public WorkIqException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="WorkIqException"/> class.</summary>
        /// <param name="message">Actionable error message.</param>
        /// <param name="innerException">Cause of the failure.</param>
        public WorkIqException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
