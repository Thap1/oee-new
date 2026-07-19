using OeeNew.Infrastructure.Identity;
using Xunit;

namespace OeeNew.Application.Tests.Auth;

public class JwksDocumentBuilderTests
{
    [Fact]
    public void Build_ExposesOneJwkPerValidationKey_WithoutPrivateKeyMaterial()
    {
        using var provider = new RsaJwtSigningKeyProvider();
        provider.RotateKey(); // now has current + previous => 2 keys

        var document = JwksDocumentBuilder.Build(provider.GetValidationKeys());

        Assert.Equal(2, document.Keys.Count);
        foreach (var jwk in document.Keys)
        {
            Assert.Equal("RSA", jwk.KeyType);
            Assert.Equal("RS256", jwk.Algorithm);
            Assert.NotEmpty(jwk.Modulus);
            Assert.NotEmpty(jwk.Exponent);
        }

        Assert.Equal(provider.GetValidationKeys().Select(k => k.KeyId).OrderBy(x => x),
            document.Keys.Select(k => k.KeyId).OrderBy(x => x));
    }
}
