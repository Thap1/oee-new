using System.Buffers.Text;

namespace OeeNew.Infrastructure.Identity;

public static class JwksDocumentBuilder
{
    public static JwksDocument Build(IReadOnlyList<SigningKeyEntry> keys) => new()
    {
        Keys = keys.Select(ToJwk).ToList(),
    };

    private static JwkDto ToJwk(SigningKeyEntry key)
    {
        var parameters = key.Rsa.ExportParameters(includePrivateParameters: false);
        return new JwkDto
        {
            KeyId = key.KeyId,
            Modulus = Base64UrlEncode(parameters.Modulus!),
            Exponent = Base64UrlEncode(parameters.Exponent!),
        };
    }

    private static string Base64UrlEncode(byte[] bytes) => Base64Url.EncodeToString(bytes);
}
