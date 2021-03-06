﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ServerBehaviour : NetworkBehaviour
{
    public const double roundTime = 10;
    public double endTime;
    public GameState state = GameState.Connecting;
    public int roundNumber = 0;

    private struct PlayerActionStruct
    {
        public int damage;
        public int heal;
        public int evade;
    }

    public List<GameObject> players = new List<GameObject>();

    // Use this for initialization
    void Start () {
        state = GameState.Connecting;
	}

    private void StartRound()
    {
        state = GameState.Round;
        endTime = Time.fixedTime + roundTime;
        Debug.Log("Starting Round");
        StartPlayerRound(players[0]);
        StartPlayerRound(players[1]);
    }

    private void StartPlayerRound(GameObject player)
    {
        player.GetComponent<GameController>().ready = false;
        player.GetComponent<GameController>().didFire = false;
        player.GetComponent<GameController>().Rpc_StartRound(roundNumber++);
    }

    public void Animate(PlayerState[] nextStates, int shooterID)
    {
        state = GameState.Animation;
        for (int i = 0; i < players.Count; i++)
        {
            GameController player = players[i].GetComponent<GameController>();
            player.ready = false;
            player.EnemySelectedCards.Clear();
            player.enemyHealth = players[1 - i].GetComponent<GameController>().health;
            foreach (int card in players[1 - i].GetComponent<GameController>().SelectedCards){
                player.EnemySelectedCards.Add(card);
            }
            player.Rpc_Animate(nextStates[0], nextStates[1], shooterID);
        }
    }

    public PlayerState[] ApplyActions()
    {
        PlayerActionStruct[] playerActions = new PlayerActionStruct[2];
        for (int i = 0; i < players[0].GetComponent<GameController>().SelectedCards.Count; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                int cdIndex = players[j].GetComponent<GameController>().SelectedCards[i];
                if (cdIndex < 0)
                    continue;
                Card card = GameObject.Find("CardDesk").GetComponent<CardDesk>().cardDesk[cdIndex];
                switch (card._ActionType)
                {
                    case Action.DamageBoth:
                        playerActions[j].damage += (int)card._value;
                        playerActions[1 - j].damage += (int)card._value;
                        break;
                    case Action.DamageEnemy:
                        playerActions[1 - j].damage += (int)card._value;
                        break;
                    case Action.DamageSelf:
                        playerActions[j].damage += (int)card._value;
                        break;
                    case Action.Evade:
                        playerActions[j].evade += (int)card._value;
                        break;
                    case Action.Heal:
                        playerActions[j].heal += (int)card._value;
                        break;
                }
            }
        }
        PlayerState[] playerStates = new PlayerState[2];
        for (int j = 0; j < 2; j++)
        {
            int playerHP = players[j].GetComponent<GameController>().health -
            Mathf.Max(0, playerActions[j].damage - playerActions[j].evade) +
            playerActions[j].heal;
            playerHP = Mathf.Min(players[j].GetComponent<GameController>()._maxHealth, playerHP);
            if (playerHP > players[j].GetComponent<GameController>().health)
                playerStates[j] = PlayerState.Healing;
            else if (playerHP < players[j].GetComponent<GameController>().health)
                playerStates[j] = PlayerState.Bleeding;
            else
                playerStates[j] = PlayerState.Idle;
            players[j].GetComponent<GameController>().health = playerHP;
        }
        return playerStates;
    }

    public void FinishRound(int shooterID)
    {
        Debug.Log("Finishing round");
        if (state.Equals(GameState.Round))
        {
            PlayerState[] nextStates = ApplyActions();
            if (players[0].GetComponent<GameController>().health>0 && players[1].GetComponent<GameController>().health>0)
                Animate(nextStates, shooterID);
            else
                state = GameState.Finish;
        }
    }


    private void StartTimer()
    {
        state = GameState.Timer;
        foreach(GameObject player in players)
        {
            player.GetComponent<GameController>().ready = false;
            player.GetComponent<GameController>().Rpc_StartTimer();
        }
    }

    private void UpdatePlayerGameStates()
    {
        foreach(GameObject player in players)
        {
            player.GetComponent<GameController>().serverState = state;
        }
    }

    private void InitPlayers()
    {
        foreach (GameObject player in players)
        {
            GameController playerController = player.GetComponent<GameController>();
            playerController.health = playerController._maxHealth;
            playerController.enemyHealth = playerController._maxHealth;
            player.GetComponent<GameController>().Rpc_InitPlayerState();
        }
    }

    // Update is called once per frame
    void Update () {
        if (isServer)
        {
            if (state.Equals(GameState.Start)) {
                bool pl2ready = players[1].GetComponent<GameController>().ready;
                bool pl1ready = players[0].GetComponent<GameController>().ready;
                if (pl1ready && pl2ready)
                {
                    InitPlayers();
                    StartTimer();
                }
            }
            else if (state.Equals(GameState.Animation))
            {
                bool pl2ready = players[1].GetComponent<GameController>().ready;
                bool pl1ready = players[0].GetComponent<GameController>().ready;
                if (pl1ready && pl2ready)
                {
                    StartTimer();
                }
            }
            else if (state.Equals(GameState.Timer))
            {
                bool pl2ready = players[1].GetComponent<GameController>().ready;
                bool pl1ready = players[0].GetComponent<GameController>().ready;
                if (pl1ready && pl2ready)
                {
                    StartRound();
                }
            }
            else if (state.Equals(GameState.Round))
            {
                if (endTime < Time.fixedTime)
                {
                    FinishRound(-1);
                }
            }
            else if (state.Equals(GameState.Finish))
            {
                int winner = -1;
                if (players[0].GetComponent<GameController>().health > 0)
                    winner = 0;
                if (players[1].GetComponent<GameController>().health > 0)
                    winner = 1;
                foreach (GameObject player in players)
                {
                    player.GetComponent<GameController>().ready = false;
                    player.GetComponent<GameController>().Rpc_Finish(winner);
                }
                state = GameState.Start;
            }
            UpdatePlayerGameStates();
            Debug.Log(state);
        }
	}
}
