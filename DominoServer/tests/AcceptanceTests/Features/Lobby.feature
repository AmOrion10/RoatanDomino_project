Feature: Lobby

# Création
Scenario: Un client crée un lobby public
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 envoie une requête pour créer un lobby public
	Then Un nouveau lobby pour le client 1 est créé 
	And Il existe un chat associé pour le lobby
	And Le client 1 reçoit une confirmation avec le lobby créé
	And Le client 1 est informé qu'il a rejoint le chat du lobby 
	And Le client 1 est dans le chat du lobby

Scenario: Un client crée un lobby privé
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 envoie une requête pour créer un lobby privé
	Then Un nouveau lobby pour le client 1 est créé 
	And Il existe un chat associé pour le lobby
	And Le client 1 reçoit une confirmation avec le lobby créé
	And Le client 1 est informé qu'il a rejoint le chat du lobby 
	And Le client 1 est dans le chat du lobby

Scenario: Un client crée un lobby alors qu'il est dans un lobby
	Given Un client d'id 1 connecté au serveur et logged in
	And Le client 1 a déjà un lobby
	When Le client 1 envoie une requête pour créer un lobby public
	Then Le client 1 reçoit une erreur de type "CreationLobbyFailed"

# Rejoignement
Scenario: Un client rejoint un lobby créé par un autre client
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 9 connecté au serveur et logged in
	And Le client 1 a déjà un lobby
	When Le client 9 envoie une requête pour rejoindre le lobby
	Then Le client 9 rejoint le lobby
	And Le client 9 reçoit les informations du lobby avec la liste des joueurs
	And Tous les clients sont notifiés de l'arrivé du client de excepté le client 9
	And Le client 9 est informé qu'il a rejoint le chat du lobby 
	And Le client 9 est dans le chat du lobby

Scenario: Un client essaye de rejoindre un lobby complet
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 9 connecté au serveur et logged in
	And Le client 1 a déjà un lobby avec 1 places
	And Le lobby est complet
	When Le client 9 envoie une requête pour rejoindre le lobby
	Then Le client 9 reçoit une erreur de type "LobbyFull"

Scenario: Un client essaye de rejoindre un lobby alors qu'il est dans un lobby
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 9 connecté au serveur et logged in
	And Le client 1 a déjà un lobby
	And Le client 9 a rejoint le lobby
	When Le client 9 envoie une requête pour rejoindre le lobby
	Then Le client 9 reçoit une erreur de type "AlreadyInALobby"

Scenario: Un client essaye de rejoindre un lobby avec un code valide
	Given Deux clients d'id 1 et 2 sont connectés et logged in
	And Le client 1 a déjà un lobby de code "ABCDE"
	When Le client 2 envoie une requête pour rejoindre le lobby avec le code "ABCDE"
	Then Le client 2 reçoit les informations du lobby avec la liste des joueurs
	And Tous les clients sont notifiés de l'arrivé du client de excepté le client 2
	And Le client 2 est informé qu'il a rejoint le chat du lobby 
	And Le client 2 est dans le chat du lobby

Scenario: Un client essaye de rejoindre un lobby avec un code non existant
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 envoie une requête pour rejoindre le lobby avec le code "ABCDE"
	Then Le client 1 reçoit une erreur de type "LobbyNotFound"

Scenario: Un client essaye de rejoindre un lobby plein avec un code valide
	Given Trois clients d'id 1, 2 et 3 sont connectés et logged in
	And Le client 1 a déjà un lobby de code "ABCDEF" avec 2 places
	And Le client 2 a rejoint le lobby
	When Le client 3 envoie une requête pour rejoindre le lobby avec le code "ABCDEF"
	Then Le client 3 reçoit une erreur de type "LobbyFull"

# Quitte
Scenario: Un client quitte un lobby et il y a au moins un autre client
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 9 connecté au serveur et logged in
	And Le client 1 a déjà un lobby
	And Le client 9 a rejoint le lobby
	When Le client 9 envoie une requête pour quitter le lobby
	Then Le client 9 quitte le lobby
	And Le client 9 reçoit une confirmation
	And Tous les clients sont notifiés du départ du client excepté le client 9
	And Tous les clients sont notifiés du nouveau créateur du lobby excepté le client 9
	And Le client 9 n'est plus dans le chat du lobby
	And Le client 9 reçoit l information qu il a quitté le chat du lobby

Scenario: Un client quitte un lobby et il n'y avait que lui dans ce lobby
	Given Un client d'id 1 connecté au serveur et logged in
	And Le client 1 a déjà un lobby
	When Le client 1 envoie une requête pour quitter le lobby
	Then Le lobby est supprimé
	And Le client 1 reçoit une confirmation
	And Le chat associé au lobby est supprimé
	And Le client 1 reçoit l information qu il a quitté le chat du lobby

Scenario: Un client souhaite quitter un lobby mais il n'est pas dans un lobby
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 9 connecté au serveur et logged in
	And Le client 1 a déjà un lobby
	When Le client 9 envoie une requête pour quitter le lobby
	Then Le client 9 reçoit une erreur de type "PlayerNotInALobby"

Scenario: Un client quitte un lobby avec un timer déjà en marche avec 2 personnes
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	And Le client 1 a déjà un lobby avec 2 places
	And Le client 2 a rejoint le lobby
	And Les clients 1 et 2 sont prêts
	And Le timer d'avant partie a débuté
	When Le client 1 envoie une requête pour quitter le lobby
	Then Le timer annulé est envoyé aux clients sauf au client 1
	And Le client 1 n'est plus dans le chat du lobby
	And Le client 1 reçoit l information qu il a quitté le chat du lobby

Scenario: Un client quitte un lobby avec un timer déjà en marche avec 3 personnes
	Given Trois clients d'id 1, 2 et 3 sont connectés et logged in
	And Le client 1 a déjà un lobby avec 3 places
	And Deux clients 2 et 3 ont rejoint le lobby
	And Les trois clients 1, 2 et 3 sont prêts
	And Le timer d'avant partie a débuté
	When Le client 1 envoie une requête pour quitter le lobby
	Then Un timer est envoyé aux clients sauf au client 1
	And Le client 1 n'est plus dans le chat du lobby
	And Le client 1 reçoit l information qu il a quitté le chat du lobby

# Obtention de tous les lobby
Scenario: Un client souhaite obtenir la liste des lobby et il existe au moins un lobby
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 9 connecté au serveur et logged in
	And Le client 9 a déjà un lobby
	When Le client 1 envoie une requête pour obtenir la liste des lobby
	Then La liste des lobby est retourné au client 1

Scenario: Un client souhaite obtenir la liste des lobby et il n'existe aucun lobby
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 envoie une requête pour obtenir la liste des lobby
	Then La liste vide des lobby est retourné au client 1

Scenario: Un client souhaite obtenir la liste des lobby et il n'existe qu'un lobby privé
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	And Le client 2 a déjà un lobby privé
	When Le client 1 envoie une requête pour obtenir la liste des lobby
	Then La liste vide des lobby est retourné au client 1

# Obtention de tous les lobby avec un nom spécifié
Scenario: Un client souhaite obtenir la liste des lobby avec un nom spécifique et il existe au moins un lobby
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 9 connecté au serveur et logged in
	And Le client 9 a déjà un lobby
	When Le client 1 envoie une requête pour obtenir la liste des lobby avec un nom spécifique
	Then La liste des lobby est retourné au client 1

Scenario: Un client souhaite obtenir la liste des lobby avec un nom spécifique et il n'existe aucun lobby
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 envoie une requête pour obtenir la liste des lobby avec un nom spécifique
	Then La liste vide des lobby est retourné au client 1

Scenario: Un client souhaite obtenir la liste des lobby avec un nom spécifique et il n'existe qu'un lobby privé
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	And Le client 2 a déjà un lobby privé
	When Le client 1 envoie une requête pour obtenir la liste des lobby avec un nom spécifique
	Then La liste vide des lobby est retourné au client 1

Scenario: Un client souhaite obtenir la liste des lobby avec un nom spécifique et il existe au moins un lobby mais le nom est différent du nom spécifié par l'utilisateur
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 9 connecté au serveur et logged in
	And Le client 9 a déjà un lobby
	When Le client 1 envoie une requête pour obtenir la liste des lobby avec un nom différent du lobby existant
	Then La liste vide des lobby est retourné au client 1

# Se met prêt
Scenario: Un client dans un lobby se met prêt pour démarrer la partie
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	And Le client 1 a déjà un lobby
	And Le client 2 a rejoint le lobby
	When Le client 1 envoie une requête pour changer son état de préparation 
	Then Tous les clients sont notifiés de l'état prêt du client 1 
	And Un timer est envoyé aux clients

Scenario: Un client se met prêt et il y a déjà un client de prêt dans un lobby pour 4 personnes
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	And Le client 1 a déjà un lobby avec 4 places
	And Le client 2 a rejoint le lobby
	And Le client 1 est déjà prêt
	When Le client 2 envoie une requête pour changer son état de préparation 
	Then Tous les clients sont notifiés de l'état prêt du client 2  
	And Le timer envoyé aux clients est inchangé

Scenario: Un client se met prêt et tous les autres clients le sont déjà
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	And Le client 1 a déjà un lobby avec 2 places
	And Le client 2 a rejoint le lobby
	And Le client 1 est déjà prêt
	When Le client 2 envoie une requête pour changer son état de préparation
	Then Tous les clients sont notifiés de l'état prêt du client 2  
	And Le timer le plus court est envoyé aux clients

Scenario: Un client ne se met plus prêt
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	And Le client 1 a déjà un lobby
	And Le client 2 a rejoint le lobby
	And Le client 1 est déjà prêt
	When Le client 1 envoie une requête pour changer son état de préparation
	Then Tous les clients sont notifiés de l'état non prêt du client 1  
	And Le timer annulé est envoyé aux clients

Scenario: Un client tout seul dans un lobby se met prêt
	Given Un client d'id 1 connecté au serveur et logged in
	And Le client 1 a déjà un lobby
	When Le client 1 envoie une requête pour changer son état de préparation
	Then Le timer annulé est envoyé aux clients

Scenario: Un client tout seul dans un lobby est prêt et un client rejoint
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	And Le client 1 a déjà un lobby
	And Le client 1 est déjà prêt
	When Le client 2 envoie une requête pour rejoindre le lobby
	Then Un timer est envoyé aux clients
	And Le client 2 est notifié de l'état prêt du client 1

# Lobby classé
Scenario: Un client crée une partie classée car aucune partie classée n'existe
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 envoie une requête pour rejoindre une partie classée
	Then Un nouveau lobby pour le client 1 est créé 
	And Il existe un chat associé pour le lobby
	And Le client 1 reçoit une confirmation avec le lobby classée créé
	And Le client 1 est informé qu'il a rejoint le chat du lobby 
	And Le client 1 est dans le chat du lobby

Scenario: Un client rejoint une partie classée créé par un autre client
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	And Le client d'id 1 a déjà un lobby classée
	When Le client 2 envoie une requête pour rejoindre une partie classée
	Then Le client 2 reçoit les informations du lobby avec la liste des joueurs
	And Tous les clients sont notifiés de l'arrivé du client de excepté le client 2
	And Le client 2 est informé qu'il a rejoint le chat du lobby 
	And Le client 2 est dans le chat du lobby

Scenario: Un client créé une partie classée car les parties existantes sont remplies
	Given Trois clients d'id 1, 2 et 3 sont connectés et logged in
	Given Deux clients d'id 4 et 5 sont connectés et logged in
	And Le client d'id 1 a déjà un lobby classée
	And Deux clients 2 et 3 ont rejoint le lobby
	And Le client 4 a rejoint le lobby
	When Le client 5 envoie une requête pour rejoindre une partie classée
	Then Le client 5 reçoit une confirmation avec le lobby classée créé
	And Le client 5 est informé qu'il a rejoint le chat du lobby 
	And Le client 5 est dans le chat du lobby
