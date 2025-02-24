Feature: Friendship

Scenario: Un utilisateur demande en ami un utilisateur via son login
	Given Un client d'id 1 connecté au serveur et logged in
	And Il existe un utilisateur nommé "JM1" 
	When Le client 1 envoie une demande d'ami à "JM1"
	Then Il existe bien une demande d'ami de la part du client 1

Scenario: Un utilisateur accepte la demande d'ami
	Given Un client d'id 2 connecté au serveur et logged in
	And Il existe un utilisateur nommé "JM2" 
	And Le client 2 a une demande d'ami de "JM2"
	When Le client 2 accepte la demande d'ami de "JM2"
	Then Le client 2 est bien ami avec "JM2"
	And Il n existe plus de demande d ami entre le client 2 et "JM2"

Scenario: Un utilisateur refuse la demande d'ami
	Given Un client d'id 2 connecté au serveur et logged in
	And Il existe un utilisateur nommé "JM3" 
	And Le client 2 a une demande d'ami de "JM3"
	When Le client 2 refuse la demande d'ami de "JM3"
	Then Le client 2 n'est bien pas ami avec "JM3"
	And Il n existe plus de demande d ami entre le client 2 et "JM3"

Scenario: Un utilisateur demande en ami un utilisateur non existant
	Given Un client d'id 1 connecté au serveur et logged in
	And Il n'existe pas un utilisateur nommé "JM4" 
	When Le client 1 envoie une demande d'ami à "JM4"
	Then Le client 1 reçoit une erreur de type "UserNotFound"

Scenario: Un utilisateur accepte une demande d'ami non existante
	Given Un client d'id 2 connecté au serveur et logged in
	And Il existe un utilisateur nommé "JM5" 
	And Le client 2 n a pas de demande d ami de "JM5"
	When Le client 2 accepte la demande d'ami de "JM5"
	Then Le client 2 reçoit une erreur de type "NoFriendRequest"

Scenario: Un utilisateur refuse une demande d'ami non existante
	Given Un client d'id 2 connecté au serveur et logged in
	And Il existe un utilisateur nommé "JM6" 
	And Le client 2 n a pas de demande d ami de "JM6"
	When Le client 2 refuse la demande d'ami de "JM6"
	Then Le client 2 reçoit une erreur de type "NoFriendRequest"

Scenario: Un utilisateur demande en ami un utilisateur avec lequel il est déjà ami
	Given Un client d'id 1 connecté au serveur et logged in
	And Il existe un utilisateur nommé "JM7" 
	And Le client 1 est déjà ami avec l'utilisateur nommé "JM7"
	When Le client 1 envoie une demande d'ami à "JM7"
	Then Le client 1 reçoit une erreur de type "AlreadyFriend"

Scenario: Un utilisateur demande en ami un qui lui demandé en ami
	Given Un client d'id 1 connecté au serveur et logged in
	And Il existe un utilisateur nommé "JM8" 
	And Le client 1 a une demande d'ami de "JM8"
	When Le client 1 envoie une demande d'ami à "JM8"
	Then Le client 1 est bien ami avec "JM8"
	And Il n existe plus de demande d ami entre le client 1 et "JM8"

Scenario: Un utilisateur accepte en ami un utilisateur non existant
	Given Un client d'id 1 connecté au serveur et logged in
	And Il n'existe pas un utilisateur nommé "JM9" 
	When Le client 1 accepte la demande d'ami de "JM9"
	Then Le client 1 reçoit une erreur de type "UserNotFound"

Scenario: Un utilisateur refuse en ami un utilisateur non existant
	Given Un client d'id 1 connecté au serveur et logged in
	And Il n'existe pas un utilisateur nommé "JM10" 
	When Le client 1 refuse la demande d'ami de "JM10"
	Then Le client 1 reçoit une erreur de type "UserNotFound"

Scenario: Un utilisateur demande à voir ses demandes d'ami avec au moins une demande d'ami
	Given Un client d'id 1 connecté au serveur et logged in
	And Il existe un utilisateur nommé "JM11"
	And Il existe un utilisateur nommé "JM12"
	And Le client 1 a une demande d'ami de "JM11"
	And Le client 1 a une demande d'ami de "JM12"
	When Le client 1 demande à voir ses demandes d'ami
	Then Le client 1 reçoit bien ses 2 demandes d'ami

Scenario: Un utilisateur demande à voir ses demandes d'ami avec aucune demande d'ami
	Given Un client d'id 1 connecté au serveur et logged in
	And Le client 1 n a aucune demande d ami
	When Le client 1 demande à voir ses demandes d'ami
	Then Le client 1 reçoit bien ses 0 demandes d'ami

Scenario: Un utilisateur supprime un ami de sa liste d'ami
	Given Un client d'id 1 connecté au serveur et logged in
	And Il existe un utilisateur nommé "JM13"
	And Le client 1 est déjà ami avec l'utilisateur nommé "JM13"
	When Le client 1 demande à supprimer son amitié avec l'utilisateur "JM13"
	Then Le client 1 n est plus ami avec l utilisateur "JM13"
	
Scenario: Un utilisateur supprime un ami qui n'est pas dans sa liste d'ami
	Given Un client d'id 1 connecté au serveur et logged in
	And Il existe un utilisateur nommé "JM14"
	And Le client 1 n est pas ami avec l utilisateur "JM14"
	When Le client 1 demande à supprimer son amitié avec l'utilisateur "JM14"
	Then Le client 1 reçoit une erreur de type "NotInFriendsList"

Scenario: Un utilisateur supprime un ami qui n'est pas existant
	Given Un client d'id 1 connecté au serveur et logged in
	And Il n'existe pas un utilisateur nommé "JM15"
	When Le client 1 demande à supprimer son amitié avec l'utilisateur "JM15"
	Then Le client 1 reçoit une erreur de type "UserNotFound"

Scenario: Un utilisateur veut récupérer sa liste d'ami avec au moins un ami
	Given Un client d'id 1 connecté au serveur et logged in
	And Il existe un utilisateur nommé "JM16"	
	And Il existe un utilisateur nommé "JM17"
	And Le client 1 est déjà ami avec l'utilisateur nommé "JM16"
	And Le client 1 est déjà ami avec l'utilisateur nommé "JM17"
	When Le client 1 demande sa liste d'ami
	Then Le client 1 reçoit bien ses 2 amis

Scenario: Un utilisateur veut récupérer sa liste d'ami avec aucun ami
	Given Un client d'id 1 connecté au serveur et logged in
	And Le client 1 n'a aucun ami
	When Le client 1 demande sa liste d'ami
	Then Le client 1 reçoit bien ses 0 amis