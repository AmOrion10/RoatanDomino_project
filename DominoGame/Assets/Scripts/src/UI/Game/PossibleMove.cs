using System;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

using UnityEngine;

public class PossibleMove : MonoBehaviour
{
    public event Action<MoveDto> MoveSelectedEvent;
    public MoveDto Move { get; private set; }

    public void SetUp(MoveDto move)
    {
        Move = move;
    }

    public void MoveSelected()
    {
        MoveSelectedEvent?.Invoke(Move);
    }
}
