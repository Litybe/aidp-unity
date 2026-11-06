using System.Collections.Generic;
using NUnit.Framework;

namespace Aidp.Playtest.Tests
{
    [PlaytestProbe("regprobe")]
    public sealed class RegProbe : IPlaytestProbe
    {
        public IReadOnlyDictionary<string, object> Read() => new Dictionary<string, object> { ["v"] = 7 };
    }

    [PlaytestProbe("regprobe")]
    public sealed class RegProbeDuplicate : IPlaytestProbe
    {
        public IReadOnlyDictionary<string, object> Read() => null;
    }

    [PlaytestProbe("empty")]
    public sealed class RegEmptyProbe : IPlaytestProbe
    {
        public IReadOnlyDictionary<string, object> Read() => null;
    }

    [PlaytestCommand("regcmd")]
    public sealed class RegCommand : IPlaytestCommand
    {
        public static float Last;

        public void Execute(PlaytestArgs args) => Last = args.RequireFloat("v");
    }

    [PlaytestCommand("notacommand")]
    public sealed class RegNotACommand
    {
    }

    public sealed class RegistryTests
    {
        [Test]
        public void FindsProbesAndCommandsAndReportsProblems()
        {
            var registry = new PlaytestRegistry(new[] { typeof(RegProbe), typeof(RegProbeDuplicate), typeof(RegCommand), typeof(RegNotACommand), typeof(RegEmptyProbe) });
            CollectionAssert.AreEquivalent(new[] { "regprobe", "empty" }, registry.ProbeNames);
            CollectionAssert.AreEquivalent(new[] { "regcmd" }, registry.CommandNames);
            Assert.AreEqual(2, registry.Problems.Count);
        }

        [Test]
        public void ReadsProbeFields()
        {
            var registry = new PlaytestRegistry(new[] { typeof(RegProbe), typeof(RegEmptyProbe) });
            Assert.IsTrue(registry.TryRead("regprobe", "v", out object v, out _));
            Assert.AreEqual(7, v);
            Assert.IsFalse(registry.TryRead("regprobe", "zz", out _, out string e1));
            StringAssert.Contains("no field", e1);
            Assert.IsFalse(registry.TryRead("empty", "v", out _, out string e2));
            StringAssert.Contains("no subject", e2);
            Assert.IsFalse(registry.TryRead("missing", "v", out _, out string e3));
            StringAssert.Contains("unknown probe", e3);
        }

        [Test]
        public void ExecutesCommands()
        {
            var registry = new PlaytestRegistry(new[] { typeof(RegCommand) });
            Scenario s = ScenarioParser.Parse("{\"id\":\"a\",\"setup\":[{\"cmd\":\"regcmd\",\"v\":4.5}]}");
            registry.Execute(s.Setup[0]);
            Assert.AreEqual(4.5f, RegCommand.Last);
            var unknown = new ScenarioCommand("zzz", new PlaytestArgs(null));
            Assert.Throws<PlaytestException>(() => registry.Execute(unknown));
        }

        [Test]
        public void ScansLoadedAssembliesThatReferenceTheRuntime()
        {
            PlaytestRegistry registry = PlaytestRegistry.FromLoadedAssemblies();
            CollectionAssert.Contains(registry.CommandNames, "regcmd");
        }

        [Test]
        public void IdsAndKeys()
        {
            Assert.IsTrue(PlaytestPaths.IsValidId("issue-214-ca2"));
            Assert.IsFalse(PlaytestPaths.IsValidId("../x"));
            Assert.IsTrue(KeyNames.IsValid("rightArrow"));
            Assert.IsTrue(KeyNames.IsValid("gamepad.leftStick.left"));
            Assert.IsFalse(KeyNames.IsValid("gamepad.nope"));
            Assert.IsFalse(KeyNames.IsValid("none"));
        }
    }
}
