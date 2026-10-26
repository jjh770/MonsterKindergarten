using System;
using UnityEngine;
using UnityEngine.UI;

// 로그인 화면의 배경을 여러 장 중 하나로 골라 보여 준다. 배경마다 그 그림에 맞는 자리에 슬라임이
// 앉아 있어야 하므로, 슬라임 묶음은 배경과 짝으로 씬에 만들어 두고 고른 짝만 켠다.
//
// 슬라임 묶음은 배경 이미지의 자식이고 앵커를 배경 비율 좌표로 잡는다. 배경은 AspectRatioFitter로
// 화면을 덮게 늘어나므로, 어느 화면 비율에서도 슬라임이 그림의 같은 자리에 같은 크기로 붙는다.
public sealed class LoginBackgroundVariants : MonoBehaviour
{
    [Serializable]
    private sealed class Variant
    {
        public Sprite Background;
        [Tooltip("이 배경에 맞춰 놓은 슬라임 묶음입니다. 배경 이미지의 자식으로 둡니다.")]
        public GameObject Slimes;
    }

    [SerializeField] private Image _background;
    [SerializeField] private Variant[] _variants;

    private void Awake()
    {
        if (_background == null || _variants == null || _variants.Length == 0)
        {
            Debug.LogError("로그인 배경 후보가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        int chosen = UnityEngine.Random.Range(0, _variants.Length);
        for (int i = 0; i < _variants.Length; i++)
        {
            Variant variant = _variants[i];
            if (variant.Slimes != null) variant.Slimes.SetActive(i == chosen);
            if (i == chosen && variant.Background != null) _background.sprite = variant.Background;
        }

        ApplyOutlines(_variants[chosen].Slimes);
    }

    // 게임 안 슬라임처럼 흰 테두리를 두른다. 테두리 셰이더는 이미지마다 재질 사본에 스프라이트
    // 영역을 넘겨야 그려지므로, 고른 묶음의 슬라임에만 한 번 적용한다.
    private static void ApplyOutlines(GameObject slimes)
    {
        if (slimes == null) return;

        foreach (SlimeOutlineImage outline in slimes.GetComponentsInChildren<SlimeOutlineImage>(true))
        {
            outline.Apply(outline.GetComponent<Image>().sprite, outlined: true, special: false);
        }
    }
}
