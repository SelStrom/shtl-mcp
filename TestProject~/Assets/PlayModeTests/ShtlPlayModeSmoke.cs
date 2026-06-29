using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// PlayMode-smoke для верификации run_tests mode=PlayMode + PlayModeOptionsGuard (DisableDomainReload).
public class ShtlPlayModeSmoke
{
    [UnityTest]
    public IEnumerator Enters_Play_And_Passes()
    {
        Assert.IsTrue(Application.isPlaying, "тест выполняется в Play mode");
        yield return null; // один кадр в play
        Assert.IsTrue(Application.isPlaying);
    }
}
