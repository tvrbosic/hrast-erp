namespace HrastERP.SharedKernel.Exceptions;

public sealed class ForbiddenException(string message = "Access is forbidden.")
    : Exception(message);
