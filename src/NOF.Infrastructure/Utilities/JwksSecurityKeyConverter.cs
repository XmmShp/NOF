using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace NOF.Infrastructure;

public static class JwksSecurityKeyConverter
{
    public static SecurityKey[] ToSecurityKeys(JwkKeyDocument[] jwkKeys)
    {
        var keys = new List<SecurityKey>();

        foreach (var jwk in jwkKeys)
        {
            if (jwk.Kty != "RSA" || string.IsNullOrWhiteSpace(jwk.N) || string.IsNullOrWhiteSpace(jwk.E))
            {
                continue;
            }

            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Base64UrlDecode(jwk.N),
                Exponent = Base64UrlDecode(jwk.E)
            });

            keys.Add(new RsaSecurityKey(rsa) { KeyId = jwk.Kid });
        }

        return keys.ToArray();
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2:
                output += "==";
                break;
            case 3:
                output += "=";
                break;
        }

        return Convert.FromBase64String(output);
    }
}
