using System.Collections;
using System.Collections.Generic;
using DSDK.Logger;
using DSDK.UISystem;
using UnityEngine;

public class SampleMenu : UIPanelSingleton<SampleMenu>
{
    public virtual void DoSomething()
    {
        DLogger.LogDebug("DoSomething");
    }
}
