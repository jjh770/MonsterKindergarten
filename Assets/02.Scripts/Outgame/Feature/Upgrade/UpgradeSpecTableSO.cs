using UnityEngine;

// SO는 데이터 클래스를 유니티에서 에셋처럼 관리할 수 있게 해주는 기능을 가지고 있다.
// 코드 수정 없이 프로젝트 뷰에서 수정가능하므로 편하게 편집할 수 있다.
[CreateAssetMenu(fileName = "UpgradeSpecTableSO", menuName = "Scriptable Objects/UpgradeSpecTableSO")]
public class UpgradeSpecTableSO : ScriptableObject
{
    [SerializeField] public UpgradeSpecData[] Datas;
}
