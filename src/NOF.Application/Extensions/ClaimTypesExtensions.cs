using System.Security.Claims;

namespace NOF;

public static partial class __NOF_Application_Extensions__
{
    extension(ClaimTypes)
    {
        /// <summary>
        /// Custom claim type for permissions, separate from standard Role claims.
        /// </summary>
        public static string Permission => "NOF.Permission";
    }
}
