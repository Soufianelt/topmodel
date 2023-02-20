﻿using TopModel.Core;
using TopModel.Utils;

namespace TopModel.Generator.Javascript;

public static class JavascriptUtils
{
    public static string GetPropertyTypeName(this IProperty property, IEnumerable<Class>? availableClasses = null)
    {
        if (property is CompositionProperty cp)
        {
            return cp.Kind switch
            {
                "object" => cp.Composition.Name,
                "list" or "async-list" => $"{cp.Composition.Name}[]",
                string _ when cp.DomainKind!.TS!.Type.Contains("{composition.name}") => cp.DomainKind.TS.Type.ParseTemplate(cp),
                string _ => $"{cp.DomainKind.TS.Type}<{{composition.name}}>".ParseTemplate(cp)
            };
        }

        var fp = (IFieldProperty)property;

        if (fp.Domain.TS == null)
        {
            throw new ModelException(fp.Domain, $"Le type Typescript du domaine doit être renseigné.");
        }

        var fixedType = fp.Domain.TS.Type.ParseTemplate(fp);

        var prop = fp is AliasProperty alp ? alp.Property : fp;

        if (prop is AssociationProperty { Association.EnumKey: not null } ap && (availableClasses == null || availableClasses.Contains(ap.Association)))
        {
            fixedType = $"{ap.Association.Name}{ap.Property.Name}";

            if (fp is AliasProperty { AsList: true })
            {
                fixedType += "[]";
            }
        }
        else if (prop == prop.Class?.EnumKey && (availableClasses == null || availableClasses.Contains(prop.Class)))
        {
            fixedType = $"{prop.Class.Name}{prop.Name}";

            if (fp is AliasProperty { AsList: true })
            {
                fixedType += "[]";
            }
        }

        if (fp is AliasProperty { Property: AssociationProperty { Type: AssociationType.ManyToMany or AssociationType.OneToMany } } && fixedType != fp.Domain.TS.Type.ParseTemplate(fp))
        {
            fixedType += "[]";
        }

        return fixedType;
    }

    public static bool IsJSReference(this Class classe)
    {
        return classe.EnumKey != null || classe.Reference && !classe.ReferenceKey!.Domain.AutoGeneratedValue;
    }

    public static List<(string Import, string Path)> GroupAndSort(this IEnumerable<(string Import, string Path)> imports)
    {
        return imports
             .GroupBy(i => i.Path)
             .Select(i => (Import: string.Join(", ", i.Select(l => l.Import).Distinct().OrderBy(x => x)), Path: i.Key))
             .OrderBy(i => i.Path.StartsWith(".") ? i.Path : $"...{i.Path}")
             .ToList();
    }

    public static void WriteReferenceDefinition(FileWriter fw, Class classe)
    {
        fw.Write("export const ");
        fw.Write(classe.Name.ToFirstLower());
        fw.Write(" = {type: {} as ");
        fw.Write(classe.Name);
        fw.Write(", valueKey: \"");
        fw.Write(classe.ReferenceKey!.Name.ToFirstLower());
        fw.Write("\", labelKey: \"");
        fw.Write(classe.DefaultProperty?.Name.ToFirstLower());
        fw.Write("\"} as const;\r\n");
    }
}
