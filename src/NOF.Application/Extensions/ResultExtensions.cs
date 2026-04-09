using NOF.Contract;
using NOF.Domain;

namespace NOF.Application;

/// <summary>
/// Extension methods for the NOF.Application layer.
/// </summary>
public static partial class NOFApplicationExtensions
{
    extension(Result)
    {
        /// <summary>Creates a failed result from a <see cref="Failure"/> descriptor.</summary>
        /// <param name="failure">The failure descriptor.</param>
        /// <returns>A failed result.</returns>
        public static FailResult Fail(Failure failure)
        {
            return Result.Fail(failure.ErrorCode, failure.Message);
        }
    }
}
