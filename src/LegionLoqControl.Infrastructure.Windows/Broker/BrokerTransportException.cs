namespace LegionLoqControl.Infrastructure.Windows.Broker;

public sealed class BrokerTransportException : Exception
{
    public BrokerTransportException(string errorCode)
        : base(errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ErrorCode = errorCode;
    }

    public BrokerTransportException(string errorCode, Exception innerException)
        : base(errorCode, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
