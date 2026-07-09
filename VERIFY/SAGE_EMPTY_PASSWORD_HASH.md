# Hash de mot de passe vide - Sage 100c

Si vous êtes bloqué hors d'une base de données Sage (mot de passe `Administrateur` perdu ou inconnu), vous pouvez réinitialiser son mot de passe à "vide" (chaîne vide) en utilisant une requête SQL directe, sans connaître l'algorithme de hachage de Sage.

Voici l'empreinte cryptographique (hash) exacte pour un mot de passe **vide** (récupérée depuis la base `BIJOU` standard) :

```sql
0xB5EF72BA0E63C8419198EC651C7E1F153CC047B7F5627A4388B9343ED59EBAF8
```

### Comment l'utiliser ("Pass-the-Hash")

Si vous n'arrivez plus à vous connecter via les Objets Métiers ou l'interface Sage sur une base de développement, exécutez la requête SQL suivante sur la base concernée (ex: `DISTRI_DEMO`) :

```sql
UPDATE F_PROTECTIONCPTA 
SET PROT_Hash = 0xB5EF72BA0E63C8419198EC651C7E1F153CC047B7F5627A4388B9343ED59EBAF8 
WHERE PROT_User = '<Administrateur>';
```

Après avoir exécuté cette requête, vous pourrez vous connecter avec le nom d'utilisateur `<Administrateur>` et **laisser le champ mot de passe vide**.

> **Note de sécurité** : N'utilisez cette manipulation que sur des environnements de développement ou de test pour ne pas compromettre la sécurité d'une base de production ou écraser un mot de passe client.
