﻿using Microsoft.Extensions.Logging;
using TopModel.Core;
using TopModel.Core.FileModel;
using TopModel.Generator.Core;

namespace TopModel.Generator.Jpa;

/// <summary>
/// Générateur de fichiers de modèles JPA.
/// </summary>
public class JpaEnumGenerator : GeneratorBase<JpaConfig>
{
    private readonly ILogger<JpaEnumGenerator> _logger;

    public JpaEnumGenerator(ILogger<JpaEnumGenerator> logger, ModelConfig modelConfig)
        : base(logger)
    {
        _logger = logger;
    }

    public override string Name => "JpaEnumGen";

    protected bool FilterClass(Class classe)
    {
        return !classe.Abstract && Config.CanClassUseEnums(classe, Classes.ToList());
    }
    public override IEnumerable<string> GeneratedFiles => Files.Values.SelectMany(f => f.Classes.Where(FilterClass))
        .SelectMany(c => Config.Tags.Intersect(c.Tags).SelectMany(tag => GetEnumProperties(c).Select(p => GetFileName(p, c, tag)))).Distinct();

    protected string GetFileName(IFieldProperty property, Class classe, string tag)
    {
        return Config.GetEnumFileName(property, classe, tag);
    }

    private IEnumerable<IFieldProperty> GetEnumProperties(Class classe)
    {
        List<IFieldProperty> result = new();
        if (classe.EnumKey != null && Config.CanClassUseEnums(classe, prop: classe.EnumKey))
        {
            result.Add(classe.EnumKey);
        }

        var uks = classe.UniqueKeys.Where(uk => uk.Count == 1 && Config.CanClassUseEnums(classe, Classes, uk.Single())).Select(uk => uk.Single());
        result.AddRange(uks);
        return result;
    }

    protected override void HandleFiles(IEnumerable<ModelFile> files)
    {
        foreach (var file in files)
        {
            foreach (var classe in file.Classes.Where(FilterClass))
            {
                foreach (var tag in Config.Tags.Intersect(classe.Tags))
                {
                    HandleClass(classe, tag);
                }
            }
        }
    }

    protected void HandleClass(Class classe, string tag)
    {
        foreach (var p in GetEnumProperties(classe))
        {
            WriteEnum(p, classe, tag);
        }
    }

    private void WriteEnum(IFieldProperty property, Class classe, string tag)
    {
        var packageName = Config.GetEnumPackageName(classe, tag);
        using var fw = new JavaWriter(Config.GetEnumFileName(property, classe, tag), _logger, packageName, null);
        fw.WriteLine();
        var codeProperty = classe.EnumKey!;
        fw.WriteDocStart(0, $"Enumération des valeurs possibles de la propriété {codeProperty.NamePascal} de la classe {classe.NamePascal}");
        fw.WriteDocEnd(0);
        fw.WriteLine($@"public enum {Config.GetEnumName(property, classe)} {{");
        var i = 0;
        foreach (var value in classe.Values.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            i++;
            var isLast = i == classe.Values.Count();
            if (classe.DefaultProperty != null)
            {
                fw.WriteDocStart(1, $"{value.Value[classe.DefaultProperty]}");
                fw.WriteDocEnd(1);
            }

            fw.WriteLine(1, $"{value.Value[property]}{(isLast ? ";" : ",")}");
        }

        fw.WriteLine("}");
    }
}