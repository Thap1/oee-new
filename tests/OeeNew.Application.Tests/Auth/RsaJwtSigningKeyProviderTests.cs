using OeeNew.Infrastructure.Identity;
using Xunit;

namespace OeeNew.Application.Tests.Auth;

public class RsaJwtSigningKeyProviderTests
{
    [Fact]
    public void GetValidationKeys_ReturnsOnlyCurrentKey_BeforeAnyRotation()
    {
        using var provider = new RsaJwtSigningKeyProvider();

        var keys = provider.GetValidationKeys();

        Assert.Single(keys);
        Assert.Equal(provider.GetSigningKey().KeyId, keys[0].KeyId);
    }

    [Fact]
    public void RotateKey_KeepsPreviousKeyValidatable_ForOverlapWindow()
    {
        using var provider = new RsaJwtSigningKeyProvider();
        var keyBeforeRotation = provider.GetSigningKey();

        provider.RotateKey();

        var validationKeys = provider.GetValidationKeys();
        Assert.Equal(2, validationKeys.Count);
        Assert.Contains(validationKeys, k => k.KeyId == keyBeforeRotation.KeyId);
        Assert.NotEqual(keyBeforeRotation.KeyId, provider.GetSigningKey().KeyId);
    }

    [Fact]
    public void RotateKey_Twice_DropsOldestKey_KeepingOnlyTwo()
    {
        using var provider = new RsaJwtSigningKeyProvider();
        var firstKey = provider.GetSigningKey();

        provider.RotateKey();
        var secondKey = provider.GetSigningKey();
        provider.RotateKey();

        var validationKeys = provider.GetValidationKeys();
        Assert.Equal(2, validationKeys.Count);
        Assert.DoesNotContain(validationKeys, k => k.KeyId == firstKey.KeyId);
        Assert.Contains(validationKeys, k => k.KeyId == secondKey.KeyId);
    }
}
