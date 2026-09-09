using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.Caching;

public static class CacheErrors
{
    public static readonly Error SerializationFailed =
        Error.Unexpected("Cache.SerializationFailed", "Failed to serialize or deserialize the cached value.");

    public static readonly Error OperationFailed =
        Error.Unexpected("Cache.OperationFailed", "A cache operation failed unexpectedly.");
}
