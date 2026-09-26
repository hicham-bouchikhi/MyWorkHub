using MyWorkHub.Core.Abstractions;

namespace MyWorkHub.Core.Tests.Abstractions;

public sealed class CredentialKeysTests
{
    // The values are persisted (encrypted) in the user's credential store: renaming one silently
    // "forgets" a saved secret. They are pinned to the keys the pre-rewrite app used.
    [Fact]
    public void Should_keep_the_persisted_key_values_stable()
    {
        Assert.Equal(
            ["AZDO_PAT", "TCL_USER", "TCL_PASSWORD", "PEOPLENET_USER", "PEOPLENET_PASSWORD", "MWORK_USER", "MWORK_PASSWORD"],
            [
                CredentialKeys.AZURE_DEVOPS_PAT,
                CredentialKeys.TCL_USER,
                CredentialKeys.TCL_PASSWORD,
                CredentialKeys.PEOPLENET_USER,
                CredentialKeys.PEOPLENET_PASSWORD,
                CredentialKeys.MWORK_USER,
                CredentialKeys.MWORK_PASSWORD,
            ]);
    }
}
