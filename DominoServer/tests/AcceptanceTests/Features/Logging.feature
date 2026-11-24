Feature: Logging

Scenario: Un client se connecte avec les bons identifiants
	Given Un client d'id 1 connecté au serveur
	And Un client inscrit avec l'email "test@test.com", le login "test" et le mdp "Password8"
	When Le client 1 demande à se connecter avec les bons identifiants
	Then Le client 1 est connecté
	And Le client 1 reçoit son token et son utilisateur

Scenario: Un client se connecte avec un login inconnu
	Given Un client d'id 1 connecté au serveur
	And Un client inscrit avec l'email "test@test.com", le login "test" et le mdp "Password8"
	When Le client 1 demande à se connecter avec un login inconnu
	Then Le client 1 reçoit une erreur de type "UserNotFound"

Scenario: Un client se connecte avec un mauvais mot de passe
	Given Un client d'id 1 connecté au serveur
	And Un client inscrit avec l'email "test@test.com", le login "test" et le mdp "Password8"
	When Le client 1 demande à se connecter avec un mauvais mot de passe
	Then Le client 1 reçoit une erreur de type "WrongLoggingOrPassword"

Scenario: Un client se connecte deux fois
	Given Un client d'id 1 connecté au serveur
	And Un client inscrit avec l'email "test@test.com", le login "test" et le mdp "Password8"
	And Le client 1 est logged in
	When Le client 1 demande à se connecter avec les bons identifiants
	Then Le client 1 reçoit une erreur de type "UserAlreadyLoggedIn"

Scenario: Deux clients essaye de se connecter au même compte
	Given Un client d'id 1 connecté au serveur
	And Un client d'id 9 connecté au serveur
	And Un client inscrit avec l'email "test@test.com", le login "test" et le mdp "Password8"
	And Le client 1 est logged in
	When Le client 9 demande à se connecter avec les bons identifiants
	Then Le client 9 reçoit une erreur de type "UserAlreadyLoggedIn"

Scenario: Un client se déconnecte
	Given Un client d'id 1 connecté au serveur
	And Un client inscrit avec l'email "test@test.com", le login "test" et le mdp "Password8"
	And Le client 1 est logged in
	When Le client 1 demande à se déconnecter
	Then Le client 1 est déconnecté
	And Le client 1 reçoit une confirmation
	And Le client 1 est informé qu il n est plus dans le chat global

Scenario: Un client se déconnecte sans être logged in
	Given Un client d'id 1 connecté au serveur
	And Le client 1 n'est pas logged in
	When Le client 1 demande à se déconnecter
	Then Le client 1 reçoit une erreur de type "UserNotLoggedIn"

Scenario: Un client se déconnecte de tout
	Given Deux clients d'id 1 et 2 sont connectés et logged in
	And Le client 1 a déjà un lobby
	And Le client 2 a rejoint le lobby
	When Le client 1 demande à se déconnecter
	Then Le client 1 n'est plus dans aucun lobby
	And Le client 1 est bien déconnecté de tous les chats
	And Le client 1 reçoit une confirmation
	And Tous les clients sont notifiés du départ du client excepté le client 1
	And Le client 1 est informé qu il n est plus dans le chat global

Scenario: Un client envoie qu'il est connecté
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 envoie l information qu il est connecté
	Then Le client 1 est connecté au chat global
	And Le client 1 reçoit le chat global

Scenario: Un client se déconnecte en pleine game avec 3 joueurs
	Given Le serveur est en écoute sur le port 12000
	And Trois clients d'id 1, 2 et 3 sont connectés et logged in
	And Il existe une game avec trois clients 1, 2 et 3 pour 3 joueurs
	And La partie a débuté
	When Le client 1 demande à se déconnecter
	Then Tous les clients sont notifiés du départ de la game du client excepté le client 1
	And Le client 1 est informé qu il n est plus dans le chat global

Scenario: Un client se déconnecte en pleine game avec 2 joueurs
	Given Le serveur est en écoute sur le port 12000
	And Deux clients d'id 1 et 2 sont connectés et logged in
	And Il existe une game avec le client 1 et 2 pour 2 joueurs
	And La partie a débuté
	When Le client 1 demande à se déconnecter
    Then Tous les clients sont informés que la game est finie sauf le client 1
	And Le client 1 est informé qu il n est plus dans le chat global
