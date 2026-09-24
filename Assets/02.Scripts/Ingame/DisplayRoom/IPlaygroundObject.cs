// 놀이터에 놓이는 오브젝트가 기능을 잠시 세울 수 있게 한다.
//
// 배치 모드에서 쓴다. 자리를 옮기는 동안 대포가 슬라임을 삼키거나 범퍼가
// 밀어내면, 플레이어가 잡고 있는 것과 물리가 움직이는 것이 뒤섞인다.
//
// 배치 모드를 오브젝트가 알아야 할 이유는 없다. "지금은 꺼져 있으라"는 말만
// 듣고, 그 말을 누가 왜 하는지는 PlaygroundObjectField가 안다.
public interface IPlaygroundObject
{
    void SetInteractive(bool isInteractive);
}
