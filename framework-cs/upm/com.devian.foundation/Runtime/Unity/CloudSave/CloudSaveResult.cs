using System;


namespace Devian
{
    public enum CloudSaveResult
    {
        Success = 0,
        NotFound = 1,

        NotAvailable = 10,
        AuthRequired = 11,

        TemporaryFailure = 20,
        FatalFailure = 30,
    }
}
