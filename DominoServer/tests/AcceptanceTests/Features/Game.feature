Feature: Game

Scenario: Un joueur se met prêt dans la game
    Given Deux clients d'id 1 et 2 sont connectés et logged in
    And Il existe une game avec le client 1 et 2 pour 2 joueurs
    When Le client 1 se met prêt dans la game
    Then Le client 1 est prêt dans la game
    And Le client 1 reçoit une confirmation

Scenario: Chaque joueur est prêt dans la game
    Given Deux clients d'id 1 et 2 sont connectés et logged in
    And Il existe une game avec le client 1 et 2 pour 2 joueurs
    And Le client 1 est déjà prêt dans la game
    When Le client 2 se met prêt dans la game
    Then Un timer est envoyé aux clients pour indiquer que la game commence

Scenario: Un joueur place un domino
	Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
    And Il existe une game avec le client 1 et 2 pour 2 joueurs
    And Les clients 1 et 2 sont prêt dans la game
    And La partie a débuté
	When Le client place un domino
	Then Le domino est placé et les informations de jeu sont envoyés aux joueurs

Scenario: Un joueur veut placer un domino mais le mouvement n'est pas autorisée
	Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
    And Il existe une game avec le client 1 et 2 pour 2 joueurs
    And Les clients 1 et 2 sont prêt dans la game
    And La partie a débuté
	When Le client souhaite placer un mauvais domino
	Then Le client reçoit une erreur de type "WrongPositionForDomino"

Scenario: Un joueur pioche un domino et la pioche n'est pas vide
	Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
    And Il existe une game avec le client 1 et 2 pour 2 joueurs
    And Les clients 1 et 2 sont prêt dans la game
    And La partie a débuté
    And Un client a déjà placé un domino
	When Le client pioche un domino
    Then Un domino est récupéré de la pioche et les informations de jeu sont envoyés aux joueurs

Scenario: Un joueur pioche un domino et la pioche est vide
	Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
    And Il existe une game avec le client 1 et 2 pour 2 joueurs
    And Les clients 1 et 2 sont prêt dans la game
    And La partie a débuté
    And Un client a déjà placé un domino
    And La pioche est vide
	When Le client pioche un domino
    Then Le client suivant joue

Scenario: Un joueur place un domino et la partie se finit
	Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
    And Il existe une game avec le client 1 et 2 pour 2 joueurs
    And Les clients 1 et 2 sont prêt dans la game
    And La partie a débuté
    And La pioche est vide
    And Un des joueurs n'a plus qu'un domino a posé
	When Le client place son dernier domino
    Then La partie se finit
    And Les clients sont déconnectés du chat de la game

Scenario: Un joueur se trouve bloqué
	Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
    And Il existe une game avec le client 1 et 2 pour 2 joueurs
    And Les clients 1 et 2 sont prêt dans la game
    And La partie a débuté
    And La pioche est vide
    And Un des joueurs n'a plus qu'un domino non plaçable a posé
	When L'autre client place un domino
    Then Le même client rejoue

Scenario: Deux joueurs se trouvent bloqués et la pioche est vide
	Given Le serveur est en écoute sur le port 12000
	And Trois clients d'id 1, 2 et 3 sont connectés et logged in
    And Il existe une game avec trois clients 1, 2 et 3 pour 3 joueurs
    And Trois clients 1, 2 et 3 sont prêt dans la game
    And La partie a débuté
    And La pioche est vide
    And Deux des joueurs n'a plus qu'un domino non plaçable a posé
	When L'autre client place un domino
    Then Le même client rejoue

Scenario: Deux joueurs se trouvent bloqués et la pioche n'est pas vide
	Given Le serveur est en écoute sur le port 12000
	And Trois clients d'id 1, 2 et 3 sont connectés et logged in
    And Il existe une game avec trois clients 1, 2 et 3 pour 3 joueurs
    And Trois clients 1, 2 et 3 sont prêt dans la game
    And La partie a débuté
    And Deux des joueurs n'a plus qu'un domino non plaçable a posé
	When L'autre client place un domino
    Then Le même client rejoue

Scenario: La partie se bloque après qu'un joueur ait posé un domino
	Given Le serveur est en écoute sur le port 12000
	And Trois clients d'id 1, 2 et 3 sont connectés et logged in
    And Il existe une game avec trois clients 1, 2 et 3 pour 3 joueurs
    And Trois clients 1, 2 et 3 sont prêt dans la game
    And La partie a débuté
    And La pioche est vide
    And Les joueurs ne peuvent poser aucun domino
	When L'autre client place un domino
    Then La partie est bloquée et le classement est envoyé
    And Les clients sont déconnectés du chat de la game

Scenario: La partie se bloque après qu'un joueur ait pioché le dernier domino 
	Given Le serveur est en écoute sur le port 12000
	And Trois clients d'id 1, 2 et 3 sont connectés et logged in
    And Il existe une game avec trois clients 1, 2 et 3 pour 3 joueurs
    And Trois clients 1, 2 et 3 sont prêt dans la game
    And La partie a débuté
    And La pioche a le domino 4, 4
    And Les joueurs ne peuvent poser aucun domino
	When Le client pioche un domino
    Then La partie est bloquée et le classement est envoyé
    And Les clients sont déconnectés du chat de la game

Scenario: La partie se bloque après que les joueurs aient pioché les deux derniers domino
	Given Le serveur est en écoute sur le port 12000
	And Trois clients d'id 1, 2 et 3 sont connectés et logged in
    And Il existe une game avec trois clients 1, 2 et 3 pour 3 joueurs
    And Trois clients 1, 2 et 3 sont prêt dans la game
    And La partie a débuté
    And La pioche a les domino 2, 3 et 4, 4
    And Les joueurs ne peuvent poser aucun domino
	When Le client pioche un domino
    Then La partie est bloquée et le classement est envoyé
    And Les clients sont déconnectés du chat de la game

Scenario: Le joueur joue après que deux autres joueurs aient été bloqué et la partie se finit
	Given Le serveur est en écoute sur le port 12000
	And Trois clients d'id 1, 2 et 3 sont connectés et logged in
    And Il existe une game avec trois clients 1, 2 et 3 pour 3 joueurs
    And Trois clients 1, 2 et 3 sont prêt dans la game
    And La partie a débuté
    And La pioche a les domino 2, 3 et 4, 4
    And Deux des joueurs n'a plus qu'un domino non plaçable a posé
    And Un joueur a les dominos 1, 5 et 0, 1 en mains
	When Le client place le domino 1, 5 sur l'emplacement 30, 29
    And Le client place le domino 0, 1 sur l'emplacement 30, 27
    Then Un joueur a posé tous ses dominos et le classement est envoyé
    And Les clients sont déconnectés du chat de la game

Scenario: Des joueurs font une partie classée et leur score est mis à jour
    Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
    And Les deux clients d'id 1 et 2 ont un score de classée égale à 10000
    And Il existe une game classée avec deux clients 1 et 2 pour 2 joueurs avec chaque joueur ayant un score classée de 10000
    And Les clients 1 et 2 sont prêt dans la game
    When La partie débute
    Then Le score des joueurs est mis à jour

Scenario: Deux joueurs finissent une partie classée et leur score est mis à jour
    Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
    And Les deux clients d'id 1 et 2 ont un score de classée égale à 10000
    And Il existe une game classée avec deux clients 1 et 2 pour 2 joueurs avec chaque joueur ayant un score classée de 10000
    And Les clients 1 et 2 sont prêt dans la game
    And La partie a débuté
    And La pioche est vide
    And Un joueur a les dominos 1, 5 et 0, 0 en mains
    And Un des joueurs n'a plus qu'un domino non plaçable a posé
    When Le client place son dernier domino
    Then Le score des clients est mis à jour après la partie

Scenario: Un client quitte en pleine game et il reste 2 joueurs
    Given Le serveur est en écoute sur le port 12000
	And Trois clients d'id 1, 2 et 3 sont connectés et logged in
    And Il existe une game avec trois clients 1, 2 et 3 pour 3 joueurs
    And La partie a débuté
    When Le client d'id 2 quitte la partie
    Then Tous les clients sont informés du départ du client 2 excepté le client 2
    And Les dominos du client 2 sont remis dans la pioche
    And Le client 2 ne fait plus parti de la game

Scenario: Un client quitte en pleine game et il ne reste plus qu'un joueur
    Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
    And Il existe une game avec le client 1 et 2 pour 2 joueurs
    And La partie a débuté
    When Le client d'id 1 quitte la partie
    Then La game n'existe plus
    And Tous les clients sont informés que la game est finie sauf le client 1
    And Les clients sont déconnectés du chat de la game
