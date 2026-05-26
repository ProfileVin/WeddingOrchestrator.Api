namespace WeddingOrchestrator.Api.Infrastructure;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
