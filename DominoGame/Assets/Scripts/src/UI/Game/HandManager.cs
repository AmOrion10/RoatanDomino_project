using System;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using UnityEngine;
using UnityEngine.UI;

public class HandManager : MonoBehaviour
{
    private const int MultiplicatorForDominoPosition = 15;
    [SerializeField] private GameObject dominoPrefab;

    public readonly List<DominoHand> Dominoes = new();
    public event Action<DominoDto> DominoSelectedEvent;

    private readonly List<DominoHand> possibleDominoes = new();

    public void SetUpHand(HandDto hand)
    {
        foreach (DominoDto domino in hand.Dominoes)
        {
            AddDomino(domino);
        }
    }

    public void RemoveDomino(DominoDto dominoDto)
    {
        foreach (DominoHand domino in Dominoes.ToList())
        {
            if (domino.Domino.Side1 == dominoDto.Side1 && domino.Domino.Side2 == dominoDto.Side2)
            {
                domino.DominoSelectedEvent -= DominoSelectedCallback;
                Destroy(domino.gameObject);
                Dominoes.Remove(domino);
                possibleDominoes.Clear();
                return;
            }
        }
    }

    public void AddDomino(DominoDto domino)
    {
        GameObject go = Instantiate(dominoPrefab, gameObject.transform);
        DominoHand item = go.GetComponent<DominoHand>();
        item.DominoSelectedEvent += DominoSelectedCallback;
        item.SetUp(domino);
        Dominoes.Add(item);
        possibleDominoes.Clear();
    }

    public void PutDominoInEvidence(DominoDto playableDomino)
    {
        foreach (DominoHand domino in Dominoes)
        {
            if (DominoEqual(domino, playableDomino))
            {
                domino.transform.localPosition += Vector3.up * MultiplicatorForDominoPosition;
                possibleDominoes.Add(domino);
            }
        }
    }

    public void PutSelectedDominoInEvidence(DominoDto playableDomino)
    {
        foreach (DominoHand domino in Dominoes)
        {
            if (possibleDominoes.Contains(domino))
            {
                if (DominoEqual(domino, playableDomino))
                {
                    domino.GetComponentInChildren<Outline>().enabled = true;
                }
                else
                {
                    domino.GetComponentInChildren<Outline>().enabled = false;
                }
            }
        }
    }

    public bool DominoEqual(DominoHand dominoInHand, DominoDto dominoDto)
    {
        return (dominoInHand.Domino.Side1 == dominoDto.Side1 && dominoInHand.Domino.Side2 == dominoDto.Side2)
            || (dominoInHand.Domino.Side1 == dominoDto.Side2 && dominoInHand.Domino.Side2 == dominoDto.Side1);
    }

    public void ClearDominoOutlines()
    {
        foreach (DominoHand domino in Dominoes)
        {
            domino.GetComponentInChildren<Outline>().enabled = false;
        }
    }

    private void DominoSelectedCallback(DominoDto domino)
    {
        DominoSelectedEvent?.Invoke(domino);
        PutSelectedDominoInEvidence(domino);
    }

    /*public void UnselectCallbackDomino(DominoHand domino)
    {
        domino.DominoSelectedEvent -= DominoSelectedCallback;
    }*/
}
