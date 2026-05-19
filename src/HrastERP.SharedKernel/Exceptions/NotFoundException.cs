namespace HrastERP.SharedKernel.Exceptions;

public sealed class NotFoundException(string entityName, object entityId)
    : Exception($"{entityName} with id '{entityId}' was not found.");
