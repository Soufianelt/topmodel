﻿using Microsoft.Extensions.Logging;
using TopModel.Core;
using TopModel.Generator.Core;
using TopModel.Utils;

namespace TopModel.Generator.Csharp;

public class CSharpClassGenerator : ClassGeneratorBase<CsharpConfig>
{
    private readonly ILogger<CSharpClassGenerator> _logger;

    private readonly Dictionary<string, string> _newableTypes = new()
    {
        ["IEnumerable"] = "List",
        ["ICollection"] = "List",
        ["List"] = "List",
        ["HashSet"] = "HashSet"
    };

    public CSharpClassGenerator(ILogger<CSharpClassGenerator> logger)
        : base(logger)
    {
        _logger = logger;
    }

    public override string Name => "CSharpClassGen";

    protected override string GetFileName(Class classe, string tag)
    {
        return Config.GetClassFileName(classe, tag);
    }

    protected override void HandleClass(string fileName, Class classe, string tag)
    {
        using var w = new CSharpWriter(fileName, _logger);

        GenerateUsings(w, classe, tag);
        w.WriteNamespace(Config.GetNamespace(classe, tag));
        w.WriteSummary(classe.Comment);
        GenerateClassDeclaration(w, classe, tag);
    }

    /// <summary>
    /// Génère le constructeur par recopie d'un type base.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="item">Classe générée.</param>
    private static void GenerateBaseCopyConstructor(CSharpWriter w, Class item)
    {
        if (item.Extends != null)
        {
            w.WriteLine();
            w.WriteSummary(1, "Constructeur par base class.");
            w.WriteParam("bean", "Source.");
            w.WriteLine(1, "public " + item.NamePascal + "(" + item.Extends.NamePascal + " bean)");
            w.WriteLine(2, ": base(bean)");
            w.WriteLine(1, "{");
            w.WriteLine(2, "OnCreated();");
            w.WriteLine(1, "}");
        }
    }

    /// <summary>
    /// Génère le type énuméré présentant les colonnes persistentes.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="item">La classe générée.</param>
    private static void GenerateEnumCols(CSharpWriter w, Class item)
    {
        w.WriteLine();
        w.WriteSummary(1, "Type énuméré présentant les noms des colonnes en base.");

        if (item.Extends == null)
        {
            w.WriteLine(1, "public enum Cols");
        }
        else
        {
            w.WriteLine(1, "public new enum Cols");
        }

        w.WriteLine(1, "{");

        var cols = item.Properties.OfType<IFieldProperty>().ToList();
        foreach (var property in cols)
        {
            w.WriteSummary(2, "Nom de la colonne en base associée à la propriété " + property.NamePascal + ".");
            w.WriteLine(2, $"{property.SqlName},");
            if (cols.IndexOf(property) != cols.Count - 1)
            {
                w.WriteLine();
            }
        }

        w.WriteLine(1, "}");
    }

    /// <summary>
    /// Génère les méthodes d'extensibilité.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="item">Classe générée.</param>
    private static void GenerateExtensibilityMethods(CSharpWriter w, Class item)
    {
        w.WriteLine();
        w.WriteSummary(1, "Methode d'extensibilité possible pour les constructeurs.");
        w.WriteLine(1, "partial void OnCreated();");
        w.WriteLine();
        w.WriteSummary(1, "Methode d'extensibilité possible pour les constructeurs par recopie.");
        w.WriteParam("bean", "Source.");
        w.WriteLine(1, $"partial void OnCreated({item.NamePascal} bean);");
    }

    /// <summary>
    /// Génère les flags d'une liste de référence statique.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="item">La classe générée.</param>
    private static void GenerateFlags(CSharpWriter w, Class item)
    {
        if (item.FlagProperty != null && item.Values.Any())
        {
            w.WriteLine();
            w.WriteLine(1, "#region Flags");
            w.WriteLine();
            w.WriteSummary(1, "Flags");
            w.WriteLine(1, "public enum Flags");
            w.WriteLine(1, "{");

            var flagValues = item.Values.Where(refValue => refValue.Value.ContainsKey(item.FlagProperty) && int.TryParse(refValue.Value[item.FlagProperty], out var _)).ToList();
            foreach (var refValue in flagValues)
            {
                var flag = int.Parse(refValue.Value[item.FlagProperty]);
                w.WriteSummary(2, refValue.GetLabel(item));
                w.WriteLine(2, $"{refValue.Name} = 0b{Convert.ToString(flag, 2)},");
                if (flagValues.IndexOf(refValue) != flagValues.Count - 1)
                {
                    w.WriteLine();
                }
            }

            w.WriteLine(1, "}");
            w.WriteLine();
            w.WriteLine(1, "#endregion");
        }
    }

    /// <summary>
    /// Génération de la déclaration de la classe.
    /// </summary>
    /// <param name="w">Writer</param>
    /// <param name="item">Classe à générer.</param>
    /// <param name="tag">Tag.</param>
    private void GenerateClassDeclaration(CSharpWriter w, Class item, string tag)
    {
        if (!item.Abstract)
        {
            if (item.Reference && Config.Kinetix)
            {
                if (!item.ReferenceKey!.Domain.AutoGeneratedValue)
                {
                    w.WriteAttribute("Reference", "true");
                }
                else
                {
                    w.WriteAttribute("Reference");
                }
            }

            if (item.Reference && item.DefaultProperty != null)
            {
                w.WriteAttribute("DefaultProperty", $@"nameof({item.DefaultProperty.NamePascal})");
            }

            if (Config.IsPersistent(item, tag))
            {
                var sqlName = Config.UseLowerCaseSqlNames ? item.SqlName.ToLower() : item.SqlName;
                if (Config.DbSchema != null)
                {
                    w.WriteAttribute("Table", $@"""{sqlName}""", $@"Schema = ""{Config.ResolveVariables(Config.DbSchema, tag, module: item.Namespace.Module.ToSnakeCase())}""");
                }
                else
                {
                    w.WriteAttribute("Table", $@"""{sqlName}""");
                }
            }
        }

        foreach (var annotation in Config.GetDecoratorAnnotations(item))
        {
            w.WriteAttribute(annotation);
        }

        var extends = Config.GetClassExtends(item);
        var implements = Config.GetClassImplements(item);

        if (item.Abstract)
        {
            w.Write($"public interface I{item.NamePascal}");

            if (implements.Any())
            {
                w.Write($" : {string.Join(", ", implements)}");
            }

            w.WriteLine();
            w.WriteLine("{");
        }
        else
        {
            w.WriteClassDeclaration(
                item.NamePascal,
                extends,
                implements.ToArray());

            GenerateConstProperties(w, item);
            GenerateConstructors(w, item);

            if (Config.DbContextPath == null && Config.IsPersistent(item, tag))
            {
                w.WriteLine();
                w.WriteLine(1, "#region Meta données");
                GenerateEnumCols(w, item);
                w.WriteLine();
                w.WriteLine(1, "#endregion");
            }

            if (Config.CanClassUseEnums(item, Classes))
            {
                w.WriteLine();
                GenerateEnumValues(w, item);
            }

            GenerateFlags(w, item);

            w.WriteLine();
        }

        GenerateProperties(w, item, tag);

        if (item.Abstract)
        {
            GenerateCreateMethod(w, item);
        }
        else
        {
            GenerateExtensibilityMethods(w, item);
        }

        w.WriteLine("}");
    }

    /// <summary>
    /// Génération des constantes statiques.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="item">La classe générée.</param>
    private void GenerateConstProperties(CSharpWriter w, Class item)
    {
        var consts = new List<(IFieldProperty Prop, string Name, string Code, string Label)>();

        foreach (var refValue in item.Values)
        {
            var label = refValue.GetLabel(item);

            if (!Config.CanClassUseEnums(item, Classes) && item.EnumKey != null)
            {
                var code = refValue.Value[item.EnumKey];
                consts.Add((item.EnumKey, refValue.Name, code, label));
            }

            foreach (var uk in item.UniqueKeys.Where(uk =>
                uk.Count == 1
                && Config.GetType(uk.Single()) == "string"
                && refValue.Value.ContainsKey(uk.Single())))
            {
                var prop = uk.Single();

                if (!Config.CanClassUseEnums(item, Classes, prop))
                {
                    var code = refValue.Value[prop];
                    consts.Add((prop, $"{refValue.Name}{prop}", code, label));
                }
            }
        }

        foreach (var @const in consts.OrderBy(x => x.Name.ToPascalCase(), StringComparer.Ordinal))
        {
            w.WriteSummary(1, @const.Label);
            w.WriteLine(1, $"public const {Config.GetType(@const.Prop).TrimEnd('?')} {@const.Name.ToPascalCase()} = {(Config.ShouldQuoteValue(@const.Prop) ? $@"""{@const.Code}""" : @const.Code)};");
            w.WriteLine();
        }
    }

    /// <summary>
    /// Génère les constructeurs.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="item">La classe générée.</param>
    private void GenerateConstructors(CSharpWriter w, Class item)
    {
        GenerateDefaultConstructor(w, item);
        GenerateCopyConstructor(w, item);
        GenerateBaseCopyConstructor(w, item);
    }

    /// <summary>
    /// Génère le constructeur par recopie.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="item">Classe générée.</param>
    private void GenerateCopyConstructor(CSharpWriter w, Class item)
    {
        w.WriteLine();
        w.WriteSummary(1, "Constructeur par recopie.");
        w.WriteParam("bean", "Source.");
        if (item.Extends != null)
        {
            w.WriteLine(1, "public " + item.NamePascal + "(" + item.NamePascal + " bean)");
            w.WriteLine(2, ": base(bean)");
            w.WriteLine(1, "{");
        }
        else
        {
            w.WriteLine(1, "public " + item.NamePascal + "(" + item.NamePascal + " bean)");
            w.WriteLine(1, "{");
        }

        w.WriteLine(2, "if (bean == null)");
        w.WriteLine(2, "{");
        w.WriteLine(3, "throw new ArgumentNullException(nameof(bean));");
        w.WriteLine(2, "}");
        w.WriteLine();

        foreach (var property in item.Properties)
        {
            var type = GetNewableType(property);
            if (type != null)
            {
                w.WriteLine(2, $"{property.NamePascal} = new {type}(bean.{property.NamePascal});");
            }
            else
            {
                w.WriteLine(2, $"{property.NamePascal} = bean.{property.NamePascal};");
            }
        }

        w.WriteLine();
        w.WriteLine(2, "OnCreated(bean);");
        w.WriteLine(1, "}");
    }

    private void GenerateCreateMethod(CSharpWriter w, Class item)
    {
        var writeProperties = item.Properties.Where(p => !p.Readonly);

        if (writeProperties.Any())
        {
            w.WriteLine();
            w.WriteSummary(1, "Factory pour instancier la classe.");
            foreach (var prop in writeProperties)
            {
                w.WriteParam(prop.NameCamel, prop.Comment);
            }

            w.WriteReturns(1, "Instance de la classe.");
            w.WriteLine(1, $"static abstract I{item.NamePascal} Create({string.Join(", ", writeProperties.Select(p => $"{Config.GetType(p)} {p.NameCamel} = null"))});");
        }
    }

    /// <summary>
    /// Génère le constructeur par défaut.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="item">Classe générée.</param>
    private void GenerateDefaultConstructor(CSharpWriter w, Class item)
    {
        w.WriteSummary(1, "Constructeur.");
        w.WriteLine(1, $@"public {item.NamePascal}()");

        if (item.Extends != null)
        {
            w.WriteLine(2, ": base()");
        }

        w.WriteLine(1, "{");

        var line = false;
        foreach (var property in item.Properties.OfType<CompositionProperty>())
        {
            var type = GetNewableType(property);
            if (type != null)
            {
                line = true;
                w.WriteLine(2, $"{property.NamePascal} = new {type}();");
            }
        }

        if (line)
        {
            w.WriteLine();
        }

        w.WriteLine(2, "OnCreated();");
        w.WriteLine(1, "}");
    }

    /// <summary>
    /// Génère l'enum pour les valeurs statiques de références.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="item">La classe générée.</param>
    private void GenerateEnumValues(CSharpWriter w, Class item)
    {
        var refs = item.Values.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();

        void WriteEnum(IFieldProperty prop)
        {
            w.WriteSummary(1, $"Valeurs possibles de la liste de référence {item}.");
            w.WriteLine(1, $"public enum {prop.Name.ToPascalCase()}s");
            w.WriteLine(1, "{");

            foreach (var refValue in refs)
            {
                w.WriteSummary(2, refValue.GetLabel(item));
                w.Write(2, refValue.Value[prop]);

                if (refs.IndexOf(refValue) != refs.Count - 1)
                {
                    w.WriteLine(",");
                }

                w.WriteLine();
            }

            w.WriteLine(1, "}");
        }

        WriteEnum(item.EnumKey!);

        foreach (var uk in item.UniqueKeys.Where(uk => uk.Count == 1 && Config.CanClassUseEnums(item, Classes, uk.Single())))
        {
            w.WriteLine();
            WriteEnum(uk.Single());
        }
    }

    /// <summary>
    /// Génère les propriétés.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="item">La classe générée.</param>
    /// <param name="tag">Tag.</param>
    private void GenerateProperties(CSharpWriter w, Class item, string tag)
    {
        var sameColumnSet = new HashSet<string>(item.Properties.OfType<IFieldProperty>()
            .GroupBy(g => g.SqlName).Where(g => g.Count() > 1).Select(g => g.Key));
        foreach (var property in item.Properties)
        {
            if (item.Properties.IndexOf(property) > 0)
            {
                w.WriteLine();
            }

            GenerateProperty(w, property, sameColumnSet, tag);
        }
    }

    /// <summary>
    /// Génère la propriété concernée.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="property">La propriété générée.</param>
    /// <param name="sameColumnSet">Sets des propriétés avec le même nom de colonne, pour ne pas les gérerer (genre alias).</param>
    /// <param name="tag">Tag.</param>
    private void GenerateProperty(CSharpWriter w, IProperty property, HashSet<string> sameColumnSet, string tag)
    {
        w.WriteSummary(1, property.Comment);

        var type = Config.GetType(property);

        if (!property.Class.Abstract)
        {
            if (property is IFieldProperty fp)
            {
                var prop = fp is AliasProperty alp && (!fp.Class.IsPersistent || alp.Property is AssociationProperty) ? alp.Property : fp;
                if (
                    (!Config.NoColumnOnAlias || fp is not AliasProperty || fp.Class.IsPersistent)
                    && fp is not AliasProperty { As: not null }
                    && (prop.Class.IsPersistent || fp.Class.IsPersistent)
                    && !Config.NoPersistence(tag) && !sameColumnSet.Contains(prop.SqlName)
                    && Classes.Contains(prop.Class))
                {
                    var sqlName = Config.UseLowerCaseSqlNames ? prop.SqlName.ToLower() : prop.SqlName;
                    if (!Config.GetDomainAnnotations(fp, tag).Any(a => a.TrimStart('[').StartsWith("Column")))
                    {
                        w.WriteAttribute(1, "Column", $@"""{sqlName}""");
                    }
                }

                if (fp.Required && !fp.PrimaryKey || fp is AliasProperty { PrimaryKey: true } || fp.PrimaryKey && fp.Class.PrimaryKey.Count() > 1)
                {
                    w.WriteAttribute(1, "Required");
                }

                if (Config.Kinetix)
                {
                    if (prop is AssociationProperty ap && Classes.Contains(ap.Association) && ap.Association.IsPersistent && ap.Association.Reference)
                    {
                        w.WriteAttribute(1, "ReferencedType", $"typeof({ap.Association.NamePascal})");
                    }
                    else if (fp is AliasProperty alp2 && !alp2.PrimaryKey && alp2.Property.PrimaryKey && Classes.Contains(alp2.Property.Class) && alp2.Property.Class.Reference)
                    {
                        w.WriteAttribute(1, "ReferencedType", $"typeof({alp2.Property.Class.NamePascal})");
                    }
                }

                if (Config.Kinetix)
                {
                    w.WriteAttribute(1, "Domain", $@"Domains.{fp.Domain.CSharpName}");
                }

                if (type == "string" && fp.Domain.Length != null)
                {
                    w.WriteAttribute(1, "StringLength", $"{fp.Domain.Length}");
                }

                foreach (var annotation in Config.GetDomainAnnotations(property, tag))
                {
                    w.WriteAttribute(1, annotation);
                }
            }

            if (property is CompositionProperty or AssociationProperty { Type: AssociationType.OneToMany or AssociationType.ManyToMany })
            {
                w.WriteAttribute(1, "NotMapped");
            }

            if (property.Class.IsPersistent && property.PrimaryKey && property.Class.PrimaryKey.Count() == 1)
            {
                w.WriteAttribute(1, "Key");
            }

            var defaultValue = Config.GetValue(property, Classes);

            w.WriteLine(1, $"public {type} {property.NamePascal} {{ get; set; }}{(defaultValue != "null" ? $" = {defaultValue};" : string.Empty)}");
        }
        else
        {
            w.WriteLine(1, $"{type} {property.NamePascal} {{ get; }}");
        }
    }

    /// <summary>
    /// Génération des imports.
    /// </summary>
    /// <param name="w">Writer.</param>
    /// <param name="item">Classe concernée.</param>
    /// <param name="tag">Tag.</param>
    private void GenerateUsings(CSharpWriter w, Class item, string tag)
    {
        var usings = new List<string>();

        if (!item.Abstract)
        {
            if (item.Reference && item.DefaultProperty != null)
            {
                usings.Add("System.ComponentModel");
            }

            if (item.Properties.OfType<IFieldProperty>().Any(p => p.Required || p.PrimaryKey || Config.GetType(p) == "string" && p.Domain.Length != null))
            {
                usings.Add("System.ComponentModel.DataAnnotations");
            }

            if (item.Properties.Any(p => p is CompositionProperty) ||
                item.Properties.OfType<IFieldProperty>().Any(fp =>
                {
                    var prop = fp is AliasProperty alp ? alp.Property : fp;
                    return (!Config.NoColumnOnAlias || fp is not AliasProperty) && prop.Class.IsPersistent && !Config.NoPersistence(tag) && Classes.Contains(prop.Class);
                }))
            {
                usings.Add("System.ComponentModel.DataAnnotations.Schema");
            }

            if (item.Properties.OfType<IFieldProperty>().Any() && Config.Kinetix)
            {
                usings.Add("Kinetix.Modeling.Annotations");
                usings.Add(Config.DomainNamespace);
            }

            if (item.Extends != null)
            {
                usings.Add(GetNamespace(item.Extends, tag));
            }
        }

        foreach (var @using in Config.GetDecoratorImports(item))
        {
            usings.Add(@using);
        }

        foreach (var property in item.Properties)
        {
            usings.AddRange(Config.GetDomainImports(property, tag));

            switch (property)
            {
                case AssociationProperty { Association.IsPersistent: true, Association.Reference: true } ap when Classes.Contains(ap.Association) && (Config.CanClassUseEnums(ap.Association, Classes, ap.Property) || Config.Kinetix):
                    usings.Add(GetNamespace(ap.Association, tag));
                    break;
                case AliasProperty { Property: AssociationProperty { Association.IsPersistent: true, Association.Reference: true } ap2 } when Classes.Contains(ap2.Association) && (Config.CanClassUseEnums(ap2.Association, Classes, ap2.Property) || Config.Kinetix):
                    usings.Add(GetNamespace(ap2.Association, tag));
                    break;
                case AliasProperty { PrimaryKey: false, Property: RegularProperty { PrimaryKey: true, Class.Reference: true } rp } when Classes.Contains(rp.Class) && (Config.CanClassUseEnums(rp.Class, Classes) || Config.Kinetix):
                    usings.Add(GetNamespace(rp.Class, tag));
                    break;
                case CompositionProperty cp:
                    usings.Add(GetNamespace(cp.Composition, tag));
                    break;
            }
        }

        w.WriteUsings(usings
            .Where(u => u != GetNamespace(item, tag))
            .Distinct()
            .ToArray());

        if (usings.Any())
        {
            w.WriteLine();
        }
    }

    private string GetNamespace(Class classe, string tag)
    {
        return Config.GetNamespace(classe, GetClassTags(classe).Contains(tag) ? tag : GetClassTags(classe).Intersect(Config.Tags).FirstOrDefault() ?? tag);
    }

    private string? GetNewableType(IProperty property)
    {
        if (property is CompositionProperty cp)
        {
            var type = Config.GetType(property);
            var genericType = type.Split('<').First();

            if (cp.Domain == null)
            {
                return type;
            }

            if (_newableTypes.TryGetValue(genericType, out var newableType))
            {
                return type.Replace(genericType, newableType);
            }
        }

        return null;
    }
}