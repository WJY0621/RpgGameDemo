using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class DelayActivationActivity : Activity
{
    public float seconds = 0.2f;

    public override async Task ActivateAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
        await base.ActivateAsync(ct);
    }
}
