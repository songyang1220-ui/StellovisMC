using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;

namespace PCL.Core.Test;

[TestClass]
public class DataDirectoryMigrationTest
{
    private string _root = null!;

    [TestInitialize]
    public void CreateSandbox() => _root = Directory.CreateTempSubdirectory("StellovisMC-path-test-").FullName;

    [TestCleanup]
    public void RemoveSandbox() => Directory.Delete(_root, true);

    private string Prepare() => DataDirectoryMigration.Prepare(_root);

    [TestMethod]
    public void ExistingSettingsAndAssetsMoveWithoutRecreatingPcl()
    {
        var old = Directory.CreateDirectory(Path.Combine(_root, "PCL", "Pictures")).Parent!.FullName;
        File.WriteAllText(Path.Combine(old, "config.v1.yml"), "theme: stars");
        File.WriteAllBytes(Path.Combine(old, "Pictures", "sky.png"), [1, 2, 3]);
        var data = Prepare();
        Assert.AreEqual(Path.Combine(_root, "StellovisMC"), data);
        Assert.AreEqual("theme: stars", File.ReadAllText(Path.Combine(data, "config.v1.yml")));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(data, "Pictures", "sky.png")));
        Assert.IsFalse(Directory.Exists(old));
        Assert.AreEqual(data, Prepare(), "Repeated startup must keep the migrated settings.");
    }

    [TestMethod]
    public void FreshStartupOnlyCreatesBrandedDirectory()
    {
        var data = Prepare();
        Assert.IsTrue(Directory.Exists(Path.Combine(_root, "StellovisMC")));
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "PCL")));
        Assert.AreEqual(data, Prepare());
    }

    [TestMethod]
    public void ExistingNewSettingsWinAndLegacyConflictsArePreserved()
    {
        var old = Directory.CreateDirectory(Path.Combine(_root, "PCL")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(_root, "StellovisMC")).FullName;
        File.WriteAllText(Path.Combine(old, "config.v1.yml"), "old settings");
        File.WriteAllText(Path.Combine(data, "config.v1.yml"), "new settings");
        Directory.CreateDirectory(Path.Combine(old, "Musics"));
        File.WriteAllText(Path.Combine(old, "Musics", "track.ogg"), "music");
        Prepare();
        Assert.AreEqual("new settings", File.ReadAllText(Path.Combine(data, "config.v1.yml")));
        Assert.AreEqual("music", File.ReadAllText(Path.Combine(data, "Musics", "track.ogg")));
        Assert.IsFalse(Directory.Exists(old));
        var backup = Directory.GetDirectories(_root, "StellovisMC.legacy-*").Single();
        Assert.AreEqual("old settings", File.ReadAllText(Path.Combine(backup, "config.v1.yml")));
    }

    [TestMethod]
    public void BlockedDestinationKeepsLegacyDataIntact()
    {
        var old = Directory.CreateDirectory(Path.Combine(_root, "PCL")).FullName;
        File.WriteAllText(Path.Combine(old, "config.v1.yml"), "keep me");
        File.WriteAllText(Path.Combine(_root, "StellovisMC"), "blocking file");
        Assert.Throws<IOException>(() => Prepare());
        Assert.AreEqual("keep me", File.ReadAllText(Path.Combine(old, "config.v1.yml")));
    }
}
