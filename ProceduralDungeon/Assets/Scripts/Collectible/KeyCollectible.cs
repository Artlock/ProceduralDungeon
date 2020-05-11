using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyCollectible : ACollectible {

    protected override void OnCollect()
    {
        Player.Instance.KeyCount++;
    }
}
