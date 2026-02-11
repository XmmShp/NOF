using System.Security.Claims;

namespace NOF;

public static partial class NOFApplicationExtensions
{
    extension(ClaimTypes)
    {
        /// <summary>
        /// Custom claim type for permissions, separate from standard Role claims.
        /// </summary>
        public static string Permission => "NOF.Permission";
    }
}
