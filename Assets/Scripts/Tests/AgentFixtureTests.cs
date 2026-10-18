// Agent fixture for docs/testing.md in the UnityFramework repository: shows what passes and
// what fails a test. FailsOnUnexpectedErrorLog is [Explicit] so a normal run stays green.
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentProbe.Tests
{
    public class AgentFixtureTests
    {
        [TestCase(1, 2, 3)]
        [TestCase(-1, 1, 0)]
        public void Adds(int a, int b, int sum)
        {
            Assert.That(a + b, Is.EqualTo(sum));
        }

        [Test]
        public void PassesWhenErrorLogIsExpected()
        {
            LogAssert.Expect(LogType.Error, "agent fixture: expected error");
            Debug.LogError("agent fixture: expected error");
        }

        [Test]
        public void WarningLogDoesNotFail()
        {
            Debug.LogWarning("agent fixture: warning");
        }

        [UnityTest]
        public IEnumerator UnityTestYieldsEditorFrames()
        {
            var start = Time.frameCount;
            yield return null;
            yield return null;
            Assert.That(Time.frameCount, Is.GreaterThanOrEqualTo(start));
        }

        [Test, Explicit("fails on purpose: an unexpected error log fails the test")]
        public void FailsOnUnexpectedErrorLog()
        {
            Debug.LogError("agent fixture: unexpected error");
        }
    }
}
