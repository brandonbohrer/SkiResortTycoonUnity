using UnityEngine;

public class SkierLoadoutRandomizer : MonoBehaviour
{
    [Header("Sockets")]
    [SerializeField] private Transform bodySocket;
    [SerializeField] private Transform headSocket;
    [SerializeField] private Transform leftFootSocket;
    [SerializeField] private Transform rightFootSocket;

    [Header("Body Variants (exactly 2: male/female)")]
    [SerializeField] private GameObject maleBodyPrefab;
    [SerializeField] private GameObject femaleBodyPrefab;

    [Header("Skis (pick one set; will spawn L+R)")]
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

    // If you later use pooling, call Randomize() in OnEnable() instead.
    public void Randomize()
    {
        ClearOld();

        // 1) Body: male or female (50/50)
        var bodyPrefab = (Random.value < 0.5f) ? maleBodyPrefab : femaleBodyPrefab;
        _bodyInstance = Spawn(bodyPrefab, bodySocket);

        // 2) Skis: one prefab, spawned twice (L + R)
        if (skiPrefabs != null && skiPrefabs.Length > 0)
        {
            var skiPrefab = skiPrefabs[Random.Range(0, skiPrefabs.Length)];
            _leftSki = Spawn(skiPrefab, leftFootSocket);
            _rightSki = Spawn(skiPrefab, rightFootSocket);
        }

        // 3) Headgear: optional
        if (headSocket != null &&
            headgearPrefabs != null && headgearPrefabs.Length > 0 &&
            Random.value < headgearChance)
        {
            var hatPrefab = headgearPrefabs[Random.Range(0, headgearPrefabs.Length)];
            _headgear = Spawn(hatPrefab, headSocket);
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

    private GameObject Spawn(GameObject prefab, Transform socket)
    {
        if (prefab == null || socket == null) return null;

        var go = Instantiate(prefab, socket);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }
}
