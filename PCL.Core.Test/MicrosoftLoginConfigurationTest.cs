using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Profile.Authentication;

namespace PCL.Core.Test;

[TestClass]
public class MicrosoftLoginConfigurationTest
{
    [TestMethod]
    public void LocalReleaseBuildCanInitializeMicrosoftLogin()
    {
        Assert.IsNotNull(new MicrosoftProvider(),
            "The distributed launcher must start Microsoft login without private CI variables.");
    }
}
