using System;
using System.Collections.Generic;
using UnityEngine;

// 장식장 상점의 판매 목록과 가격.
//
// 코드가 아니라 에셋에 두는 것은 이 저장소 관행이다. 밸런스를 다시 컴파일하지
// 않고 만질 수 있고, 가격이 바뀌어도 저장 데이터는 영향을 받지 않는다.
[CreateAssetMenu(
    fileName = "PlaygroundShopTable",
    menuName = "MonsterKindergarten/Playground Shop Table")]
public sealed class PlaygroundShopTableSO : ScriptableObject
{
    [Serializable]
    public sealed class ObjectEntry
    {
        [SerializeField] private EPlaygroundObjectType _type;
        [SerializeField] private string _displayName;
        [TextArea(1, 3)]
        [SerializeField] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField, Min(0f)] private double _price;

        public EPlaygroundObjectType Type => _type;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public double Price => _price;
    }

    [Serializable]
    public sealed class ThemeEntry
    {
        [SerializeField] private EBackgroundTheme _theme;
        [SerializeField] private string _displayName;
        [TextArea(1, 3)]
        [SerializeField] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField, Min(0f)] private double _price;

        public EBackgroundTheme Theme => _theme;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public double Price => _price;
    }

    [Tooltip("장식장에서 파는 놀이터 오브젝트입니다.")]
    [SerializeField] private ObjectEntry[] _objects = Array.Empty<ObjectEntry>();

    [Tooltip("메인 필드에서 파는 배경 테마입니다. 땅과 하늘은 기본 제공이라 넣지 않습니다.")]
    [SerializeField] private ThemeEntry[] _themes = Array.Empty<ThemeEntry>();

    public IReadOnlyList<ObjectEntry> Objects => _objects;
    public IReadOnlyList<ThemeEntry> Themes => _themes;
}
