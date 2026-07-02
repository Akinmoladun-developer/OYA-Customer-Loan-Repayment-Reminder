namespace OyaMicroCreditCLRRS.Domain.Exceptions;

// This throws error when a business rule is violated.
// Caught by the global exception middleware and returned as 400 Bad Request.

public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

// Throws error when a requested resources does not exist. 
// Handled by the global exception middleware and returned 404 Not Found.

public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, object id)
        : base($"{resource} with ID '{id}' was not found.") { }

    public NotFoundException(string message) : base(message) { }
}

// Throws error when caller does not the have permission to perform an action or to access a particular resource.
// Handled by the global exception middleware and returned 403 Forbidden.
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message) { }
}