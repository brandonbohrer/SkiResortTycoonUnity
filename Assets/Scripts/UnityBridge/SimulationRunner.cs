using UnityEngine;


public class SimulationRunner : MonoBehaviour
{
private float _t;


void Update()
{
_t += Time.deltaTime;
if (_t >= 1f)
{
_t = 0f;
Debug.Log("Unity + Cursor pipeline works ✅");
}
}
}