## 1.0.11
- [d31beb](https://github.com/klee-contrib/topmodel/commit/d31beb5e0d42178e62f6b19316abcbbccde8884d) Fix Initialisation enum dans le cas d'alias ou d'association : cas null

## 1.0.10
- [acddcfe](https://github.com/klee-contrib/topmodel/commit/acddcfe1ed07577a7188768d674ee805764da6d4) Fix Initialisation enum dans le cas d'alias ou d'association

## 1.0.9

- [`e01da3f`](https://github.com/klee-contrib/topmodel/commit/e01da3f1d3b8c0dc39fe1eb8e206b953efb4b882) Problème import java entre deux classes générées Fix #398

## 1.0.8

- [`ab967cd`](https://github.com/klee-contrib/topmodel/commit/ab967cd621e914618d141d62d5182f113fbc306c) Correction converter dans le cas de composition

## 1.0.7

- [#395](https://github.com/klee-contrib/topmodel/pull/395) - Accolades sur le "if liste null".

## 1.0.6

- [`e0f01b8e`](https://github.com/klee-contrib/topmodel/commit/e0f01b8ea3d404aa196cfacd85f85564462bf581) Correction régression nullable

## 1.0.5

- [`97bc094a`](https://github.com/klee-contrib/topmodel/commit/97bc094a94e52167fd0bb86d1aca5308dbfc0593)
  - Enums :
    - Les setters ne sont plus générés
    - Les valeurs sont placés en premier
    - Ajout de l'annotation `@Transiant`
    - Les DAOS ne sont plus générés
    - Les `;` en fin d'enum ne sont plus générés lorsqu'ils sont inutiles
  - L'attribut `nullable` n'est plus renseigné lorsqu'il s'agit de la valeur par défaut

BREAKING CHANGES : - Les setters ne sont plus générés - les DAOS n'étant plus générés, ceux existant seront supprimés à la première génération

## 1.0.4

- [`aafe5e0c`](https://github.com/klee-contrib/topmodel/commit/aafe5e0c0b286a610e783d41d06da9ff74232c6a) - Fix formattage hashcode

## 1.0.3

Version initiale.
