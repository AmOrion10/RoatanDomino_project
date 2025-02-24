Feature: PasswordForgotten

Scenario: Un client envoie une requête pour recevoir un code pour réinitialiser son mot de passe
    Given Un client d'id 1 connecté au serveur
    And Il existe un utilisateur avec l'adresse mail "test1@gmail.com"
    When Le client 1 demande un reset de mot de passe pour l'adresse email "test1@gmail.com"
    Then Le client 1 reçoit une confirmation
    And Un mail est envoyé à l'adresse "test1@gmail.com"

Scenario: Un client envoie une requête pour recevoir un code pour réinitialiser son mot de passe avec une adresse mail inconnue
    Given Un client d'id 1 connecté au serveur
    When Le client 1 demande un reset de mot de passe pour l'adresse email "unknown@gmail.com"
    Then Le client 1 reçoit une erreur de type "UserNotFound"

Scenario: Un client envoie une requête pour recevoir un code pour réinitialiser son mot de passe deux fois en peu de temps
    Given Un client d'id 1 connecté au serveur
    And Il existe un utilisateur avec l'adresse mail "test2@gmail.com"
    And Il existe déjà un demande de reset de mot de passe pour l'adresse email "test2@gmail.com"
    When Le client 1 demande un reset de mot de passe pour l'adresse email "test2@gmail.com"
    Then Le client 1 reçoit une erreur de type "CodeAlreadySent"

Scenario: Un client envoie une requête pour recevoir un code pour réinitialiser son mot de passe deux fois avec de la marge de temps
    Given Un client d'id 1 connecté au serveur
    And Il existe un utilisateur avec l'adresse mail "test3@gmail.com"
    And Il existe déjà un demande de reset de mot de passe pour l'adresse email "test3@gmail.com" il y a 60 secondes
    When Le client 1 demande un reset de mot de passe pour l'adresse email "test3@gmail.com"
    Then Le client 1 reçoit une confirmation
    And Un mail est envoyé à l'adresse "test3@gmail.com"

Scenario: Un client envoie son nouveau mot de passe après avoir demandé pour le réinitialiser avec un code valide et un bon mot de passe
    Given Un client d'id 1 connecté au serveur
    And Il existe un utilisateur avec l'adresse mail "test4@gmail.com"
    And Il existe déjà un demande de reset de mot de passe pour l'adresse email "test4@gmail.com"
    When Le client 1 envoie un nouveau bon mot de passe avec le code valide
    Then Le mot de passe du client a bien changé
    And Le client 1 reçoit une confirmation

Scenario: Un client envoie son nouveau mot de passe après avoir demandé pour le réinitialiser avec un code valide et un mauvais mot de passe
    Given Un client d'id 1 connecté au serveur
    And Il existe un utilisateur avec l'adresse mail "test5@gmail.com"
    And Il existe déjà un demande de reset de mot de passe pour l'adresse email "test5@gmail.com"
    When Le client 1 envoie un nouveau mauvais mot de passe avec le code valide
    Then Le mot de passe du client reste inchangé
    And Le client 1 reçoit une erreur de type "InvalidPasswordFormat"

Scenario: Un client envoie son nouveau mot de passe après avoir demandé pour le réinitialiser avec un code non valide
    Given Un client d'id 1 connecté au serveur
    And Il existe un utilisateur avec l'adresse mail "test6@gmail.com"
    And Il existe déjà un demande de reset de mot de passe pour l'adresse email "test6@gmail.com"
    When Le client 1 envoie son nouveau mot de passe avec le code non valide
    Then Le mot de passe du client reste inchangé
    And Le client 1 reçoit une erreur de type "InvalidCode"
