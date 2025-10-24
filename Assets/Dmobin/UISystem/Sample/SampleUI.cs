using System.Collections;
using System.Collections.Generic;
using DSDK.Logger;
using DSDK.UISystem;
using UnityEngine;

public class SampleUI : SampleMenu
{
    public override void DoSomething()
    {
        base.DoSomething();
        DLogger.LogInfo("DoSomething");
    }
}
