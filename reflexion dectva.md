VISION COMPLÈTE DU MODULE DÉCLARATION TVA DÉDUCTIBLE MAROC

Objectif :
Créer un module complet de préparation, contrôle et dépôt de la déclaration TVA.
Le module ne doit pas être un simple export XML, mais un processus transparent :
Création déclaration → Sélection → Calcul → Contrôle → Intégration → Export.

====================================================
1. GESTION DES DÉCLARATIONS TVA
====================================================

Premier écran d'entrée.

Objectifs :
- Créer une déclaration
- Suivre son statut
- Consulter l'historique

Exemple :

Déclarations TVA déductible

+ Nouvelle déclaration


N°      Période     Statut        Lignes      TVA          Actions
-----------------------------------------------------------------
125     06/2026     🟡 En cours    0           -            Ouvrir
124     05/2026     🔒 Clôturée    310         98 200 DH    Voir
123     04/2026     📦 Déposée     290         87 500 DH    Voir


Création :

Société :
EMA Distribution

Période :
Juin 2026

Régime :
○ Mensuel
○ Trimestriel


Après création :

Déclaration N°125
Statut : En cours


====================================================
2. ÉCRAN RÈGLEMENTS 💰
====================================================

Objectif :
Afficher les règlements pouvant générer une TVA déductible.

Sources :
- Chèques
- Virements
- Traites
- Espèces


Exemple :

Déclaration Juin 2026


Filtres :
- Mode paiement
- Date rapprochement
- Fournisseur
- Etat


Sélection | N° règlement | Date | Mode | Montant | Etat
--------------------------------------------------------
☑          CH000124       05/06  Chèque    60 000  🟢
☑          VIR00125       12/06  Virement 120 000 🟢
☐          ESP00045       15/06  Espèce     5 000 🟠


Total sélectionné :
185 000 DH


Actions :

[Voir affectations]
[Analyser TVA]


Informations importantes :
- Date paiement
- Date rapprochement
- Banque
- Mode paiement
- Montant
- Nombre de factures affectées
- Etat de contrôle


Statuts :

🟢 Eligible
🟠 Contrôle nécessaire
🔴 Bloqué


Exemples blocage :
- règlement non affecté
- fournisseur sans ICE
- règlement non comptabilisé


====================================================
3. ÉCRAN FACTURES / AFFECTATIONS 📄
====================================================

Important :
L'unité métier n'est pas la facture seule.

L'unité est :

Affectation règlement → Facture


Exemple :

Facture | Fournisseur | TTC facture | Montant payé | TVA déclarée
------------------------------------------------------------------
FA00125 | ABC SARL    | 60 000      | 40 000       | 6 667
FA00126 | XYZ SARL    | 20 000      | 20 000       | 4 000


Détail facture :

Facture FA00125


Informations Sage :

HT :
50 000

TVA :
10 000

TTC :
60 000


Paiement déclaré :

Montant affecté :
40 000


Calcul :

40 000 / 60 000

= 66,66 %


TVA déclarée :

10 000 × 66,66 %

= 6 667 DH


Permet de gérer :
- paiement partiel
- plusieurs paiements
- plusieurs taux TVA


====================================================
4. ÉCRAN CALCUL TVA 🧮
====================================================

Objectif :
Afficher le calcul avant intégration.


Exemple :

Facture     Taux       HT déclaré     TVA déclarée
--------------------------------------------------
FA00125     20%        33 333         6 667
FA00126     20%        16 667         3 333


Total TVA :
10 000 DH


Gestion multi-taux :

Facture FA00130


TVA Sage :

HT          Taux        TVA
--------------------------------
30 000      20%         6 000
10 000      10%         1 000


Paiement :
50 %


Résultat :

TVA 20% :
3 000

TVA 10% :
500


Cette étape valorise :
- lecture Sage BO
- cache facture
- proratisation correcte
- gestion multi-taux
- arrondi AwayFromZero


====================================================
5. ÉCRAN INTÉGRATION DES LIGNES ✅
====================================================

Equivalent amélioré du bouton "Intégrer" de GRFN.


Avant validation :

Intégration déclaration


Lignes sélectionnées :
245


Montant TVA :
125 430 DH


Contrôles :

✔ Factures trouvées
✔ TVA calculée
✔ Affectations valides
✔ ICE conformes


[Confirmer intégration]


Après intégration :

- Création des lignes déclaration
- Rattachement aux affectations
- Exclusion des prochaines recherches


====================================================
6. ÉCRAN CONTRÔLE DÉCLARATION 🔍
====================================================

Objectif :
Répondre à :

"Est-ce que ma déclaration est correcte avant dépôt ?"


Vue synthèse :

Contrôle déclaration Juin 2026


Répartition par source :


Décaissements fournisseurs     220 000
Espèces                          15 000
Dépenses                          8 000
Frais bancaires                   7 000
---------------------------------------
Total                           250 000


Contrôle équilibre :

✔ OK


Vue taux TVA :

20%       230 000
14%        15 000
10%         5 000


Vue anomalies :

🔴 Bloquantes :

- ICE fournisseur incorrect
- IF incorrect
- Facture absente Sage
- Montant nul


🟠 Avertissements :

- Règlement sans affectation
- Données incomplètes


====================================================
7. ÉCRAN JUSTIFICATIF LIGNE 📌
====================================================

Accessible par double clic sur une ligne.


Exemple :


Facture :
FA00125


Fournisseur :
ABC SARL


IF :
12345678

ICE :
123456789012345


Historique :


📄 Facture Sage
01/06/2026


↓

💰 Règlement
CH000124
05/06/2026


↓

🏦 Rapprochement
10/06/2026


↓

🧮 Calcul TVA


TTC facture :
60 000


Montant payé :
40 000


TVA déclarée :
6 667


Objectif :
Donner une piste d'audit complète.


====================================================
8. ÉCRAN SYNTHÈSE & EXPORT 📦
====================================================


Validation déclaration


Nombre lignes :
245


TVA totale :
125 430 DH


Etat :

✔ Contrôle terminé


Actions :

📗 Export Excel contrôle

📦 Générer XML Simpl-TVA

🔒 Clôturer déclaration


====================================================
WORKFLOW COMPLET
====================================================


Créer déclaration

        ↓

Charger les règlements

        ↓

Consulter les affectations/factures

        ↓

Calculer TVA

        ↓

Contrôler anomalies

        ↓

Intégrer les lignes

        ↓

Contrôle final

        ↓

Clôture

        ↓

Export Excel + XML


====================================================
STRUCTURE FINALE DES ÉCRANS
====================================================


🏠 Déclarations TVA

    |
    +── 💰 Règlements

    |
    +── 📄 Factures / Affectations

    |
    +── 🧮 Calcul TVA

    |
    +── ✅ Intégration lignes

    |
    +── 🔍 Contrôle déclaration

    |
    +── 📌 Justificatifs

    |
    +── 📦 Synthèse & Export


Résultat :
Le module devient un moteur déclaratif TVA complet :
- sélection des données GRF
- récupération TVA exacte depuis Sage
- calcul métier fiable
- contrôle des anomalies
- traçabilité complète
- génération conforme Simpl-TVA