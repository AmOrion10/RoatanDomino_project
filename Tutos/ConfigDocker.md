# Configuration Docker

## Sommaire
- [Configuration Docker](#configuration-docker)
  - [Sommaire](#sommaire)
  - [Présentation](#présentation)
  - [Téléchargement Docker](#téléchargement-docker)
  - [Configuration](#configuration)
  - [Base de données](#base-de-données)

## Présentation

On utilise [Docker](https://docs.docker.com/get-started/overview/) pour gérer la BDD localement. Docker est un système qui permet de créer, déployer et gérer des applications dans ce qu'on appelle des "conteneurs". Ceux-ci contiennent des sortes d'OS appelés "Image". Une image peut se basée sur une autre puis une autre etc...

Il existe des tonnes d'image disponible sur [ce lien](https://hub.docker.com/). Nous allons utilisé une image pour MySql qui contient le minimum pour faire fonctionner MySql dans un environnement très similaire à la production.

## Téléchargement Docker
Peu importe la plateforme sur laquelle vous êtes, vous pouvez télécharger [Docker Desktop](https://www.docker.com/products/docker-desktop/). Si vous êtes sur Windows je vous conseille fortement d'utiliser WSL2.

## Configuration 
À la racine du repository git, il y a un fichier nommé `docker-compose.yml` celui-ci contient les paramètres des 2 images que l'on va utiliser à savoir `mysql:8.3.0` et `phpmyadmin:5.2.1`. Si vous voulez en savoir plus sur ce type de fichier c'est par [ici](https://docs.docker.com/compose/).

1) Vérifiez que vous pouvez exécuter la commande `docker --help` dans votre terminal favori. 
2) Placez vous à la racine du répertoire git puis exécuter la commande `docker-compose up -d`. Cette commande lit le fichier docker-compose.yml puis va "pull" (=télécharger) les images nécessaires et lancer les 2 images localement (-d indique que l'on veut que ce soit fait en arrière plan).
3) Attendez un peu le téléchargement et le lancement des images puis vérifier qu'il n'y a pas d'erreur dans les logs en les lisants à l'aide de la commande `docker-compose logs db`.
4) Si jamais vous voulez arrêter les containers en train de tourner vous pouvez exécuter la commande `docker-compose down`

Une fois cette configuration effectué au moins une fois vous pouvez passer par l'application Docker Desktop pour lancer et arrêter les containers. 

⚠ Pour les gérer depuis l'application Docker Desktop, il **ne faut pas arrêter** les containers avec la commande `docker-compose down`. Si jamais vous l'avez fait, vous pouvez refaire un `docker-compose up -d` et arrêter les containers via l'application.

## Base de données
Comme expliqué précédemment on va utiliser deux images. Une pour MySql et une autre pour PhpMyAdmin. Si vous avez lancer le container vous pouvez normalement accéder à l'url [http://localhost:8080](http://localhost:8080), une page de PhpMyAdmin s'ouvre. Les identifiants sont les suivants : 
- Server : db
- User : user
- Password : password