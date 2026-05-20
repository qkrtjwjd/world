using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectPool : MonoBehaviour
{
    public static EffectPool Instance { get; private set; }

    private readonly Dictionary<GameObject, Queue<GameObject>> _pool
        = new Dictionary<GameObject, Queue<GameObject>>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Play(GameObject prefab, Vector3 pos, Quaternion rot = default)
    {
        if (prefab == null) return;

        var fx = Dequeue(prefab) ?? Instantiate(prefab);

        fx.transform.SetPositionAndRotation(pos, rot);
        fx.SetActive(true);

        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
            ps.Play();

        StartCoroutine(ReturnWhenDone(fx, prefab));
    }

    GameObject Dequeue(GameObject prefab)
    {
        if (_pool.TryGetValue(prefab, out var q) && q.Count > 0)
            return q.Dequeue();
        return null;
    }

    void Enqueue(GameObject fx, GameObject prefab)
    {
        fx.SetActive(false);
        if (!_pool.ContainsKey(prefab)) _pool[prefab] = new Queue<GameObject>();
        _pool[prefab].Enqueue(fx);
    }

    IEnumerator ReturnWhenDone(GameObject fx, GameObject prefab)
    {
        float maxDuration = 3f;
        bool  found       = false;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var m = ps.main;
            if (m.loop) continue;
            float d = m.duration + m.startLifetime.constantMax;
            if (!found || d > maxDuration) { maxDuration = d; found = true; }
        }

        yield return new WaitForSeconds(maxDuration + 0.2f);
        if (fx != null) Enqueue(fx, prefab);
    }
}
