using UnityEngine;

public class SkierLoadoutRandomizer : MonoBehaviour
{
    [Header("Sockets (in SkierRoot prefab)")]
    [SerializeField] private Transform bodySocket;
    [SerializeField] private Transform headSocket;
    [SerializeField] private Transform leftFootSocket;
    [SerializeField] private Transform rightFootSocket;

    [Header("Body Variants (exactly 2: male/female)")]
    [SerializeField] private GameObject maleBodyPrefab;
    [SerializeField] private GameObject femaleBodyPrefab;

    [Header("Skis (pick one; spawn twice)")]
    [SerializeField] private GameObject[] skiPrefabs;

    [Header("Headgear (optional)")]
    [SerializeField] private GameObject[] headgearPrefabs;
    [Range(0f, 1f)] [SerializeField] private float headgearChance = 0.6f;

    private GameObject _bodyInstance;
    private GameObject _leftSki;
    private GameObject _rightSki;
    private GameObject _headgear;

    private void Awake()
    {
        Randomize();
    }

    public void Randomize()
    {
        ClearOld();

        // 0) Resolve sockets from THIS prefab (SkierRoot), not from body instance
        // (If you assigned them in inspector, these are already good.
        //  This just makes it resilient if you forget.)
        var resolvedBodySocket = bodySocket ? bodySocket : FindSocket(transform, "BodySocket");
        var resolvedLeft = leftFootSocket ? leftFootSocket : FindSocket(transform, "LeftFootSocket");
        var resolvedRight = rightFootSocket ? rightFootSocket : FindSocket(transform, "RightFootSocket");
        var resolvedHead = headSocket ? headSocket : FindSocket(transform, "HeadSocket");

        if (resolvedBodySocket == null)
        {
            Debug.LogError("BodySocket not found/assigned on SkierRoot.");
            return;
        }

        // 1) Body
        var bodyPrefab = (Random.value < 0.5f) ? maleBodyPrefab : femaleBodyPrefab;
        _bodyInstance = Spawn(bodyPrefab, resolvedBodySocket, zeroScale: false);

        // 2) Skis
        if (skiPrefabs != null && skiPrefabs.Length > 0)
        {
            var skiPrefab = skiPrefabs[Random.Range(0, skiPrefabs.Length)];
            _leftSki = Spawn(skiPrefab, resolvedLeft, zeroScale: false);
            _rightSki = Spawn(skiPrefab, resolvedRight, zeroScale: false);
        }

        // 3) Headgear
        if (resolvedHead != null &&
            headgearPrefabs != null && headgearPrefabs.Length > 0 &&
            Random.value < headgearChance)
        {
            var hatPrefab = headgearPrefabs[Random.Range(0, headgearPrefabs.Length)];
            _headgear = Spawn(hatPrefab, resolvedHead, zeroScale: false);
        }
    }

    private void ClearOld()
    {
        if (_bodyInstance) Destroy(_bodyInstance);
        if (_leftSki) Destroy(_leftSki);
        if (_rightSki) Destroy(_rightSki);
        if (_headgear) Destroy(_headgear);

        _bodyInstance = _leftSki = _rightSki = _headgear = null;
    }

    private GameObject Spawn(GameObject prefab, Transform socket, bool zeroScale)
    {
        if (prefab == null || socket == null) return null;

        var go = Instantiate(prefab, socket);

        // Snap position/rotation to socket
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        // DO NOT force scale unless you 100% know everything is authored at scale=1
        if (zeroScale)
            go.transform.localScale = Vector3.one;

        return go;
    }

    private static Transform FindSocket(Transform root, string socketName)
    {
        if (root == null) return null;

        // Robust: search all descendants by name
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == socketName)
                return all[i];
        }
        return null;
    }
}
