using Lean.Pool;
using System.Collections;
using TMPro;
using UnityEngine;
using Utility;

public class MaplestoryFloater : MonoBehaviour
{
    [SerializeField] private TextMeshPro _text;

    public void Show(double damage)
    {
        gameObject.SetActive(true);
        _text.text = damage.ToForamttedString();

        StartCoroutine(Show_Coroutine());
    }

    private IEnumerator Show_Coroutine()
    {
        yield return new WaitForSeconds(0.8f);
        LeanPool.Despawn(gameObject);
    }
}
