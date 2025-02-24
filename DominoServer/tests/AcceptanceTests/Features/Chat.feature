Feature: Chat

Scenario: Un client envoie un message dans le chat global
	Given Trois clients d'id 1, 2 et 3 sont connectés et logged in
	When Le client d'id 1 envoie un message dans le chat global
	Then Tous les clients connectés au chat global le reçoivent

Scenario: Trois clients communiquent dans un chat
	Given Trois clients d'id 1, 2 et 3 sont connectés et logged in
	And Trois clients d'id 1, 2 et 3 sont connecté à un chat
	When Le client 1 envoie un message dans le chat
	Then Tous les clients connectés au chat le reçoivent
