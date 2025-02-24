using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NFluent;
using UnityEngine.TestTools;

public class Test1
{
    // A Test behaves as an ordinary method
    [Test]
    public void Test1SimplePasses()
    {
        // Use the Assert class to test conditions
        Check.That(true).IsTrue();
        Check.That(false).IsFalse();
    }

    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    public IEnumerator Test1WithEnumeratorPasses()
    {
        // Use the Assert class to test conditions.
        // Use yield to skip a frame.
        Check.That(true).IsTrue();
        yield return null;
    }
}
