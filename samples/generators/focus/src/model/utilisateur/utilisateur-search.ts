////
//// ATTENTION CE FICHIER EST GENERE AUTOMATIQUEMENT !
////

import {EntityToType, FieldEntry2, StoreNode} from "@focus4/stores";
import {DO_CODE, DO_DATE_CREATION, DO_DATE_MODIFICATION, DO_EMAIL, DO_ID, DO_LIBELLE, DO_NUMBER} from "../../domains";

import {TypeUtilisateurCode} from "./references";

export type UtilisateurSearch = EntityToType<UtilisateurSearchEntityType>;
export type UtilisateurSearchNode = StoreNode<UtilisateurSearchEntityType>;
export interface UtilisateurSearchEntityType {
    id: FieldEntry2<typeof DO_ID, number>,
    age: FieldEntry2<typeof DO_NUMBER, number>,
    profilId: FieldEntry2<typeof DO_ID, number>,
    email: FieldEntry2<typeof DO_EMAIL, string>,
    nom: FieldEntry2<typeof DO_LIBELLE, string>,
    typeUtilisateurCode: FieldEntry2<typeof DO_CODE, TypeUtilisateurCode>,
    dateCreation: FieldEntry2<typeof DO_DATE_CREATION, string>,
    dateModification: FieldEntry2<typeof DO_DATE_MODIFICATION, string>
}

export const UtilisateurSearchEntity: UtilisateurSearchEntityType = {
    id: {
        type: "field",
        name: "id",
        domain: DO_ID,
        isRequired: false,
        label: "utilisateur.utilisateur.id"
    },
    age: {
        type: "field",
        name: "age",
        domain: DO_NUMBER,
        isRequired: false,
        defaultValue: 6l,
        label: "utilisateur.utilisateur.age"
    },
    profilId: {
        type: "field",
        name: "profilId",
        domain: DO_ID,
        isRequired: false,
        label: "utilisateur.utilisateur.profilId"
    },
    email: {
        type: "field",
        name: "email",
        domain: DO_EMAIL,
        isRequired: false,
        label: "utilisateur.utilisateur.email"
    },
    nom: {
        type: "field",
        name: "nom",
        domain: DO_LIBELLE,
        isRequired: false,
        defaultValue: "Jabx",
        label: "utilisateur.utilisateur.nom"
    },
    typeUtilisateurCode: {
        type: "field",
        name: "typeUtilisateurCode",
        domain: DO_CODE,
        isRequired: false,
        defaultValue: "ADM",
        label: "utilisateur.utilisateur.typeUtilisateurCode"
    },
    dateCreation: {
        type: "field",
        name: "dateCreation",
        domain: DO_DATE_CREATION,
        isRequired: false,
        label: "utilisateur.entityListeners.dateCreation"
    },
    dateModification: {
        type: "field",
        name: "dateModification",
        domain: DO_DATE_MODIFICATION,
        isRequired: false,
        label: "utilisateur.entityListeners.dateModification"
    }
}