# REX : Lecture des Taxes via Objets Métiers Sage 100c

Ce document capitalise les acquis de la création du `SageTaxReader` (worker autonome de lecture OM), destiné à être réutilisé dans le connecteur `.sage100-connector`.

## 1. Objets et Interfaces OM identifiés

Toutes ces informations sont sourcées depuis la documentation officielle **`sage 100c objets métiers.pdf`**.

### 1.1. Documents d'Achat
Contrairement aux ventes (bien documentées dans GOCOM), les achats utilisent leur propre factory et type :
- **Factory** : `session.FactoryDocumentAchat` (Doc. Officielle p.200, p.342)
- **Enumération Document** : `DocumentType.DocumentTypeAchatFacture` (DO_Type 16) (Doc. Officielle p.152, p.526)
- **Interface** : `IBODocumentAchat3` (Doc. Officielle p.132, p.200, p.422)

### 1.2. Extraction des Taxes (Le Cœur)
Les taxes globales calculées d'un document (Vente ou Achat) s'obtiennent **exclusivement** via l'objet `Valorisation` disponible dans les interfaces `IBODocument3` :
- `doc.Valorisation` renvoie un objet `IDocValorisation` (Doc. Officielle p.351, p.452, p.474).
- `doc.Valorisation.Taxes` renvoie une collection `IDocValoTaxes` contenant des objets `IDocValoTaxe` (Doc. Officielle p.352, p.353, p.354).
- Propriétés utiles sur `IDocValoTaxe` (Doc. Officielle p.332, p.536) :
  - `.TaxeTaux` : Le taux de la taxe en %.
  - `.TaxeBase` : La base HT assujettie (qui tient compte des escomptes, remises, etc.).
  - `.TaxeMontant` : Le montant calculé de la taxe.
  - `.TaxeTTC` : Le TTC pour ce taux.
  - `.Taxe.TA_Code` : Le code taxe Sage (ex: "C20", "D20").

> **⚠️ AVERTISSEMENT DE REFERENCE** :
> Les montants globaux HT (`doc.DO_TotalHT`) de l'en-tête ne doivent **jamais** être utilisés pour recalculer la TVA. L'OM démontre que la vraie base d'imposition (`TaxeBase`) est souvent différente du Total HT. Par exemple, sur la facture `G0110`, le HT document est de `609,07`, mais la base d'imposition est modifiée par un escompte de `6,09` et la présence de `555,00` de frais de port HT. 

## 2. Pièges rencontrés et contournés

### 2.1. Compilation et Interop
- **MSBuild requis** : L'utilisation de `dotnet build` pour résoudre `COMReference` vers `Objets100cLib` est très instable. L'application .NET 4.8 doit être buildée via MSBuild standard avec `<LangVersion>latest</LangVersion>` pour utiliser C# moderne tout en gardant une intégration COM native (x86).
- **Interface STA et timeout** : Toujours encercler les appels via un Thread paramétré en `ApartmentState.STA`. 

### 2.2 Remises globales, Frais et Multiplicité de taxes
Le HT d'une ligne d'article n'est pas forcément la base taxable. L'équation `Σbase + Σtva = TTC` n'est pas un invariant.
**Preuves issues de la base DISTRI_DEMO :**
- **Facture de Vente `25FA01371`** : Le TotalHT document est de `25947,05` (incluant 10,00 de frais). Ce HT subit un escompte global de `259,47`, donnant un HT Net de `25687,58`. Les taxes s'élèvent à `5108,03` de TVA et `1,09` de Parafiscale, amenant à un TTC parfait de `30796,70`.
- **Facture d'Achat `G0110`** : L'écart apparent de 548,54 se décompose intégralement avec les données du document :
    - Total HT document : 609,07 (dont 555,00 de frais de port HT)
    - Escompte 1% : -6,09
    - TVA Réelle (20%, 10%, 7%) : 12,39
    - Timbre TTN (Parafiscale) : 1,00
    - Écart d'arrondi : -0,37
    - Total TTC : 616,00.
    Cet exemple prouve qu'un écart apparent entre "Σ(Lignes) + TVA" et "TTC" s'explique toujours par des éléments de valorisation (frais de port non taxés, escompte, arrondi, parafiscale).

**En résumé :** L'objet COM `Valorisation.Taxes` a déjà résolu ce puzzle fiscal. Il expose les montants exacts à utiliser pour la déclaration de TVA, et l'API `IBODocument3` nous permet de lire les escomptes et frais pour justifier toute la valorisation TTC.

### 2.3. Base mono-utilisateur et verrous
- `app.Open()` échouera silencieusement avec une exception `COMException` (ex: "Ce fichier est en cours d'utilisation" ou "Le mot de passe est incorrect") si les identifiants sont erronés, ou si la base est accédée en mode exclusif par un autre processus. Le traitement des erreurs de connexion doit être soigné et remonter explicitement la non-disponibilité.

### 2.4. Accès aux Propriétés Communes
Les factures d'achat et de vente n'ont pas de classe parente forte avec toutes les propriétés métiers exposées. Il faut systématiquement utiliser du pattern matching `switch(doc)` pour accéder à `IBODocumentVente3` vs `IBODocumentAchat3` avant d'extraire la valorisation, ou utiliser l'interface de base `IBODocument3`.

## 3. Architecture Validée
Le worker `SageTaxReader.Core` est stricte :
- Dépendance COM unique (zéro DLL tierce ou GRF).
- Thread STA englobant pour `session.Open()` et `session.Close()`.
- Utilisation religieuse de `Marshal.ReleaseComObject` dans des blocs `finally`.
