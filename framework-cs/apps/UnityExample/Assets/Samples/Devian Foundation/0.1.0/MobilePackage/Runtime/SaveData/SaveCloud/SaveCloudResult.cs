using System;


namespace Devian
{
    public enum SaveCloudResult
    {
        Success = 0,
        NotFound = 1,

        NotAvailable = 10,
        AuthRequired = 11,

        TemporaryFailure = 20,
        FatalFailure = 30,
    }
}
