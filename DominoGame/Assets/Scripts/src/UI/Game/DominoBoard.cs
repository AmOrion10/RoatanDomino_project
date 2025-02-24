using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

using UnityEngine;
using UnityEngine.UI;

public class DominoBoard : MonoBehaviour
{
    [SerializeField] private List<Sprite> dots;
    [SerializeField] private Image side1;
    [SerializeField] private Image side2;

    public DominoAlignement Alignement { get; set; }
    public DominoDto Domino { get; set; }
    public Vector3 destinationPosition { get; set; }

    public void SetUp(DominoDto domino)
    {
        Domino = domino;
        if (domino.Side1 != 0) side1.sprite = dots[domino.Side1 - 1];
        if (domino.Side2 != 0) side2.sprite = dots[domino.Side2 - 1];
    }
}
