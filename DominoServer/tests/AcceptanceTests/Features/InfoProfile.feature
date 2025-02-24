Feature: InfoProfile

Scenario: Un client veut accéder à ses informations et est logged in
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 demande à accéder à ses informations
	Then Le client 1 reçoit bien ses informations
	
Scenario: Un client veut accéder à ses informations mais n'est pas logged in 
	Given Un client d'id 9 connecté au serveur 
	When Le client 9 demande à accéder à ses informations
	Then Le client 9 reçoit une erreur de type "UserNotLoggedIn"

Scenario: Un client veut modifier ses informations et est logged in
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 demande à modifier ses informations avec le login "Guillaume", la date de naissance "01/09/2009" et la nationalité "Australia"
	Then Le client 1 a bien modifié ses informations

Scenario: Un client veut modifier ses informations excepté son login et est logged in
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 demande à modifier ses informations avec le login "Client1", la date de naissance "01/09/2009" et la nationalité "Australia"
	Then Le client 1 a bien modifié ses informations

Scenario: Un client veut modifier ses informations avec un login déjà utilisé et est logged in
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	When Le client 1 demande à modifier ses informations avec le login "Client2", la date de naissance "01/09/2009" et la nationalité "Australia"
	Then Le client 1 reçoit une erreur de type "UserAlreadyExist"

Scenario: Un client veut modifier ses informations avec une date invalide et est logged in
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 demande à modifier ses informations avec le login "Guillaume", la date de naissance "01/09" et la nationalité "Australia"
	Then Le client 1 reçoit une erreur de type "InvalidDateFormat"

Scenario: Un client veut modifier ses informations avec une pays non existant et est logged in
	Given Un client d'id 8 connecté au serveur et logged in
	When Le client 8 demande à modifier ses informations avec le login "Guillaume8", la date de naissance "01/09/2009" et la nationalité "GuillaumeLand"
	Then Le client 8 reçoit une erreur de type "InvalidCountry"

Scenario: Un client veut modifier son mot de passe en renseignant son mot de passe actuel et est logged in
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 demande à modifier son mot de passe "GoodPassword8" par "GuigsLeGuigs1"
	Then Le client 1 a bien modifié son mot de passe

Scenario: Un client veut modifier son mot de passe en renseignant un mauvais mot de passe actuel et est logged in
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 demande à modifier son mot de passe "BadPassword8" par "GuigsLeGuigs1"
	Then Le client 1 reçoit une erreur de type "WrongLoggingOrPassword"

Scenario: Un client veut modifier son mot de passe en renseignant un mauvais nouveau mot de passe et est logged in
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 demande à modifier son mot de passe "GoodPassword8" par "Guigs"
	Then Le client 1 reçoit une erreur de type "InvalidPasswordFormat"

Scenario: Un client veut accéder aux informations d'un autre joueur et est logged in
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	When Le client 1 demande à accéder aux informations du client "Client2"
	Then Le client 1 reçoit bien les informations du client 2

Scenario: Un client veut accéder aux informations d'un joueur inexistant et est logged in
	Given Un client d'id 1 connecté au serveur et logged in
	When Le client 1 demande à accéder aux informations du client "Client20"
	Then Le client 1 reçoit une erreur de type "UserNotFound"

Scenario: Un client veut accéder à son historique de parties
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	And Une partie est sauvegardé avec les clients 1 et 2
	When Le client 1 demande à accéder à son historique de parties
	Then Le client 1 reçoit la liste de ses parties jouées

Scenario: Un client veut accéder aux détails d'une partie précise
	Given Un client d'id 1 connecté au serveur et logged in
	And Un client d'id 2 connecté au serveur et logged in
	And Une partie est sauvegardé avec les clients 1 et 2
	When Le client 1 demande à accéder aux détails d'une partie
	Then Le client 1 reçoit toutes les informations de la partie