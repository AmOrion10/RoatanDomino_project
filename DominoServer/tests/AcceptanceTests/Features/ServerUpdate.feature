Feature: ServerUpdate

Scenario: Les clients reçoivent l'expiration du timer du lobby
	Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 9 sont connectés et logged in
	And Le client 1 a déjà un lobby avec 2 places
	And Le client 9 a rejoint le lobby
	And Les clients 1 et 9 sont prêts
	When Le timer d'avant partie arrive à terme
	Then Les clients reçoivent l'information du début de partie
	And Le lobby est convertit en game
    And Les clients sont déconnectés du chat du lobby

Scenario: La game commence
	Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
    And Il existe une game avec le client 1 et 2 pour 2 joueurs
    And Les clients 1 et 2 sont prêt dans la game
	And Le timer de la game a débuté
    When Le timer du début de partie est arrivé à expiration
    Then Chaque joueur reçoit une liste de ses domino
	And Tous les clients sont connectés au chat de la partie
	And Un premier domino est placé automatiquement
	And Le timer de cours de partie débute
    And Chaque joueur savent qui commence à jouer
	And Le client qui commence à jouer reçoit la liste des mouvements possibles
	And Les clients reçoivent le nombre de domino de chaque joueurs

Scenario: La timer de cours de partie finit et la partie finit
	Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
    And Il existe une game avec le client 1 et 2 pour 2 joueurs
    And Les clients 1 et 2 sont prêt dans la game
	And Le timer de la game a débuté
	And Le timer d'avant partie a expiré
	And Le timer de cours de partie a débuté
    When Le timer de cours de partie est arrivé à expiration
    Then Le classement est envoyé aux joueurs
    And Les clients sont déconnectés du chat de la game

Scenario: La timer de tour d'un joueur expire et nous changeons de joueur
	Given Le serveur est en écoute sur le port 12000
	And Trois clients d'id 1, 2 et 3 sont connectés et logged in
    And Il existe une game avec trois clients 1, 2 et 3 pour 3 joueurs
    And Trois clients 1, 2 et 3 sont prêt dans la game
	And Le timer de la game a débuté
	And Le timer d'avant partie a expiré
    When Le timer de tour d'un joueur est arrivé à expiration
    Then Le joueur suivant joue
