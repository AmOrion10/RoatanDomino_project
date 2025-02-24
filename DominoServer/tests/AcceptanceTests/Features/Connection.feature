Feature: Connection

Cette page de test est pour la connexion au sens "réseau" des clients i.e. TCP
Si vous voulez voir pour la connexion au sens d'identifiant et de mot de passe => voir le fichier Logging.feature

Scenario: Connexion d'un client quand il y a de place
	Given Le serveur est en écoute sur le port 12000
	And Le serveur a encore de la place pour 1 personne
	When Un client se connecte
	Then Le serveur accepte sa connexion

Scenario: Connexion d'un client quand il n'y a plus de place
	Given Le serveur est en écoute sur le port 12000
	And Le serveur n'a plus de place
	When Un client se connecte
	Then Le serveur refuse sa connexion

Scenario: Client connecté envoie une requête
	Given Le serveur est en écoute sur le port 12000
	And Le serveur a encore de la place pour 1 personne
	And Un client est connecté au serveur
	When Le client envoie une requête au serveur
	Then La requête est bien lue par le serveur

Scenario: Déconnexion d'un client 
	Given Le serveur est en écoute sur le port 12000
	And Le serveur a encore de la place pour 1 personne
	And Un client est connecté au serveur
	When Le client se déconnecte
	Then Le client n'est plus connecté au serveur
