using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ACollectible : MonoBehaviour {

    protected abstract void OnCollect();

    // Update is called once per frame
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.transform.parent != Player.Instance.gameObject.transform)
            return;

        OnCollect();
        Destroy(gameObject);
    }
}
