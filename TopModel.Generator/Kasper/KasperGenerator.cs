﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TopModel.Core.FileModel;
using Microsoft.Extensions.Logging;

namespace TopModel.Generator.Kasper
{
    public class KasperGenerator : GeneratorBase
    {
        private readonly KasperConfig _config;
        private readonly ILogger<KasperGenerator> _logger;
        private readonly IDictionary<FileName, ModelFile> _files = new Dictionary<FileName, ModelFile>();

        public KasperGenerator(ILogger<KasperGenerator> logger, KasperConfig config)
            : base(logger, config)
        {
            _config = config;
            _logger = logger;
        }

        public override string Name => "KasperGen";

        protected override void HandleFiles(IEnumerable<ModelFile> files)
        {
            foreach (var file in files)
            {
                _files[file.Name] = file;
            }

            foreach (var file in files)
            {
                GenerateClasses(file);
            }
        }

        private void GenerateClasses(ModelFile file)
        {
            foreach (var classe in file.Classes.Where(c => !c.Properties.All(p => p is AssociationProperty)))
            {
                GenerateClass(classe);
            }
        }

        private void GenerateClass(Class classe)
        {
            var destFolder = Path.Combine(_config.SourcesDirectory, Path.Combine(_config.PackageName.Split(".")), classe.Namespace.Module.ToLower());

            using var fw = new JavaWriter(Path.Combine(destFolder, $"{classe.Name}Abstract.java"), _logger);

            var packageName = $"{_config.PackageName}.{classe.Namespace.Module.ToLower()}";
            fw.WriteLine($"package {packageName};");
            fw.WriteLine();
            fw.WriteLine($"import static {packageName}.{classe.Name}Abstract.Fields.*;");
            fw.WriteLine("import kasper.model.DtField;");
            fw.WriteLine();

            fw.WriteDocStart(0, classe.Comment);
            fw.WriteDocEnd(0);
            fw.WriteAttribute(0, "javax.persistence.MappedSuperclass");
            fw.WriteAttribute(0, "javax.persistence.Inheritance", "strategy = javax.persistence.InheritanceType.TABLE_PER_CLASS");
            fw.WriteAttribute(
                0,
                "kasperx.annotation.DtDefinition",
                $@"name = ""DT_{classe.SqlName}""",
                $@"javaClassName = ""{packageName}.{classe.Name}""",
                $@"packageName = ""{packageName}""");
            fw.WriteClassDeclaration($"{classe.Name}Abstract", $"{_config.PackageName}.DtObjectAbstract", true);

            fw.WriteLine();
            fw.WriteDocStart(1, "Nom du DT.");
            fw.WriteDocEnd(1);
            fw.WriteLine(1, $@"public static final String DEFINITION_NAME = ""DT_{classe.SqlName}"";");

            var properties = classe.Properties.OfType<IFieldProperty>().ToList();

            fw.WriteLine();
            fw.WriteLine();
            fw.WriteDocStart(1, "Noms des Fields.");
            fw.WriteDocEnd(1);
            fw.WriteLine(1, "public static enum Fields {");

            foreach (var property in properties)
            {
                fw.WriteDocStart(2, property.Comment);
                fw.WriteDocEnd(2);
                fw.WriteLine(2, $"{property.SqlName}{(properties.IndexOf(property) < properties.Count - 1 ? "," : string.Empty)}");
            }

            fw.WriteLine(1, "}");

            fw.WriteLine();
            fw.WriteDocStart(1, "SerialVersionUID.");
            fw.WriteDocEnd(1);
            fw.WriteLine(1, "private static final long serialVersionUID = 1L;");

            fw.WriteLine();
            fw.WriteDocStart(1, "Constructeur par défaut.");
            fw.WriteDocEnd(1);
            fw.WriteLine(1, $"{classe.Name}Abstract() {{");
            fw.WriteLine(2, "super();");
            fw.WriteLine(2, "init(kasper.Container.getNameSpace().getDtDefinition(DEFINITION_NAME));");
            fw.WriteLine(1, "}");

            fw.WriteLine();
            fw.WriteDocStart(1, "Retourne le serialVersionUid pour être utilisé dans l'implémentation.");
            fw.WriteReturns(1, "serialVersionUid");
            fw.WriteDocEnd(1);
            fw.WriteLine(1, "static long getSerialVersionUID() {");
            fw.WriteLine(2, "return serialVersionUID;");
            fw.WriteLine(1, "}");

            fw.WriteLine();
            fw.WriteLine(1, "final void setValue(Fields field, Object value) {");
            fw.WriteLine(2, "setValue(getField(field), value);");
            fw.WriteLine(1, "}");

            fw.WriteLine();
            fw.WriteLine(1, "final Object getValue(Fields field) {");
            fw.WriteLine(2, "return getValue(getField(field));");
            fw.WriteLine(1, "}");

            fw.WriteLine();
            fw.WriteLine(1, "final DtField getField(Fields field) {");
            fw.WriteLine(2, "return getDefinition().getField(field.toString());");
            fw.WriteLine(1, "}");

            var associations = properties.OfType<AssociationProperty>()
                .Where(ap => _files.SelectMany(f => f.Value.Classes).Contains(ap.Association)
                    && properties.OfType<AssociationProperty>().Count(p => p.Association == ap.Association) == 1);

            foreach (var property in properties)
            {
                if (property.Domain.JavaType == null)
                {
                    throw new Exception($"Le domaine {property.Domain.Name}, utilisé par la propriété {property.JavaName} de la classe {classe.Name}, doit avoir un type Java.");
                }

                fw.WriteLine();

                var propType = property.PrimaryKey ? "PRIMARY_KEY" : associations.Contains(property) ? "FOREIGN_KEY" : "DATA";
                fw.WriteDocStart(1, $"Champ : {propType}.\nRécupère la valeur de la propriété '{property.Label}'");
                fw.WriteReturns(1, $"{property.Domain.JavaType} {property.JavaName.ToFirstLower()}");
                fw.WriteDocEnd(1);

                if (property.PrimaryKey)
                {
                    fw.WriteAttribute(1, "javax.persistence.Id");
                    fw.WriteAttribute(1, "javax.persistence.SequenceGenerator", @"name = ""sequence""", $@"sequenceName = ""SEQ_{classe.SqlName}""");
                    fw.WriteAttribute(1, "javax.persistence.GeneratedValue", @"generator = ""sequence""");
                }

                fw.WriteAttribute(1, "javax.persistence.Column", $@"name = ""{property.SqlName}""");

                var kasperFieldProps = new List<string>();
                if (propType != "DATA")
                {
                    kasperFieldProps.Add($@"type = ""{propType}""");
                }

                kasperFieldProps.Add($@"domain = ""{property.Domain.Name}""");

                if (property.Required)
                {
                    kasperFieldProps.Add("notNull = true");
                }

                kasperFieldProps.Add($@"label = ""{property.Label}""");

                fw.WriteAttribute(1, "kasperx.annotation.Field", kasperFieldProps.ToArray());
                fw.WriteLine(1, $"public final {property.Domain.JavaType} get{property.JavaName}() {{");
                fw.WriteLine(2, $"return ({property.Domain.JavaType}) getValue({property.SqlName});");
                fw.WriteLine(1, "}");

                fw.WriteLine();
                fw.WriteDocStart(1, $"Champ : {propType}.\nDéfinit la valeur de la propriété '{property.Label}'");
                fw.WriteParam(property.JavaName.ToFirstLower(), property.Domain.JavaType);
                fw.WriteDocEnd(1);
                fw.WriteLine(1, $"public final void set{property.JavaName}({property.Domain.JavaType} {property.JavaName.ToFirstLower()}) {{");
                fw.WriteLine(2, $"setValue({property.SqlName}, {property.JavaName.ToFirstLower()});");
                fw.WriteLine(1, "}");
            }

            foreach (var association in associations)
            {
                fw.WriteLine();
                fw.WriteDocStart(1, $"Association : {association.Association.Name.ToFirstLower()}.");
                fw.WriteReturns(1, $"{_config.PackageName}.{association.Association.Namespace.Module.ToLower()}.{association.Association.Name}");
                fw.WriteThrows(1, $"kasper.util.KSystemException Exception système");
                fw.WriteDocEnd(1);
                fw.WriteAttribute(1, "javax.persistence.Transient");
                fw.WriteAttribute(
                    1,
                    "kasperx.annotation.Association",
                    $@"name = ""A_{classe.SqlName}_{association.Association.SqlName}""",
                    $@"fkFieldName = ""{((IFieldProperty)association).SqlName}""",
                    $@"primaryDtDefinitionName = ""DT_{association.Association.SqlName}""",
                    $@"primaryIsNavigable = true",
                    $@"primaryRole = ""{association.Association.Comment.Split("\n").First()}""",
                    $@"primaryMultiplicity = {(association.Required ? @"""1..1""" : @"""0..1""")}",
                    $@"foreignDtDefinitionName = ""DT_{classe.SqlName}""",
                    $@"foreignIsNavigable = false",
                    $@"foreignRole = ""{classe.Comment.Split("\n").First()}""",
                    $@"foreignMultiplicity = ""0..*""");
                fw.WriteLine(1, $"public {_config.PackageName}.{association.Association.Namespace.Module.ToLower()}.{association.Association.Name} get{association.Association.Name}() throws kasper.util.KSystemException {{");
                fw.WriteLine(2, $@"return ({_config.PackageName}.{association.Association.Namespace.Module.ToLower()}.{association.Association.Name}) getObject(""A_{classe.SqlName}_{association.Association.SqlName}"");");
                fw.WriteLine(1, "}");
            }

            foreach (var nn in _files.Values.SelectMany(f => f.Classes).Where(c => c.Properties.All(p => p is AssociationProperty)).Where(c => c.Properties.OfType<AssociationProperty>().First().Association == classe))
            {
                var target = ((AssociationProperty)nn.Properties.Last()).Association;

                fw.WriteLine();
                fw.WriteDocStart(1, $"Association : {target.Name.ToFirstLower()}.");
                fw.WriteReturns(1, $"kasper.model.DtCollection<{_config.PackageName}.{target.Namespace.Module.ToLower()}.{target.Name}>");
                fw.WriteThrows(1, $"kasper.util.KSystemException Exception système");
                fw.WriteDocEnd(1);
                fw.WriteAttribute(1, "javax.persistence.Transient");
                fw.WriteLine(1, $"public kasper.model.DtCollection<{_config.PackageName}.{target.Namespace.Module.ToLower()}.{target.Name}> get{target.Name}Collection() throws kasper.util.KSystemException {{");
                fw.WriteLine(2, $@"return this.<{_config.PackageName}.{target.Namespace.Module.ToLower()}.{target.Name}> getCollection(""A_{nn.SqlName}"", ""{target.Name.ToFirstLower()}"");");
                fw.WriteLine(1, "}");
            }

            fw.WriteLine("}");
        }
    }
}