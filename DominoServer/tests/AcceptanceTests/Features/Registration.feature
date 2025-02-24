Feature: Registration

Scenario: Un client veut s'inscrire avec les bons champs
	Given Un client d'id 1 connecté au serveur
	When Le client 1 demande à s inscrire avec l email "user0@user.fr", le login "login1" et le mdp "password8"
	Then Le client est inscrit
	And Le client 1 reçoit une confirmation

Scenario: Un client veut s'inscrire avec un email déjà pris
	Given Un client d'id 1 connecté au serveur
	And Un client déjà inscrit avec l'email "user1@user.fr"
	When Le client 1 demande à s inscrire avec l email "user1@user.fr", le login "login2" et le mdp "password8"
	Then Le client 1 reçoit une erreur de type "UserAlreadyExist"

Scenario: Un client veut s'inscrire avec un email avec un format incorrect
	Given Un client d'id 1 connecté au serveur
	When Le client 1 demande à s inscrire avec l email "user3@user", le login "login3" et le mdp "password8"
	Then Le client 1 reçoit une erreur de type "InvalidEmailFormat"

Scenario: Un client veut s'inscrire avec un login déjà pris
	Given Un client d'id 1 connecté au serveur
	And Un client déjà inscrit avec le login "guigs"
	When Le client 1 demande à s inscrire avec l email "guigs@user.fr", le login "guigs" et le mdp "password8"
	Then Le client 1 reçoit une erreur de type "UserAlreadyExist"

Scenario: Un client veut s'inscrire avec un login trop long
	Given Un client d'id 1 connecté au serveur
	When Le client 1 demande à s inscrire avec l email "user4@user.fr", le login "abcdefghjklmnopqrstuvxyz" et le mdp "password8"
	Then Le client 1 reçoit une erreur de type "LoginTooLong"

Scenario: Un client veut s'inscrire avec un mot de passe incorrect
	Given Un client d'id 1 connecté au serveur
	When Le client 1 demande à s inscrire avec l email "user5@user.fr", le login "login4" et le mdp "pass"
	Then Le client 1 reçoit une erreur de type "InvalidPasswordFormat"
