﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TopModel.Core.FileModel;
using TopModel.Core.Loaders;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace TopModel.Core
{
    public class ModelStore
    {
        private readonly ModelConfig _config;
        private readonly IMemoryCache _fsCache;
        private readonly ILogger<ModelStore> _logger;
        private readonly ModelFileLoader _modelFileLoader;
        private readonly IEnumerable<IModelWatcher> _modelWatchers;

        private readonly Dictionary<string, ModelFile> _modelFiles = new();

        private readonly object _puLock = new();
        private readonly HashSet<string> _pendingUpdates = new();

        public ModelStore(IMemoryCache fsCache, ModelFileLoader modelFileLoader, ILogger<ModelStore> logger, ModelConfig config, IEnumerable<IModelWatcher> modelWatchers)
        {
            _config = config;
            _fsCache = fsCache;
            _logger = logger;
            _modelFileLoader = modelFileLoader;
            _modelWatchers = modelWatchers;
        }

        public IEnumerable<Class> Classes => _modelFiles.SelectMany(mf => mf.Value.Classes);

        public IDictionary<string, Domain> Domains => _modelFiles.SelectMany(mf => mf.Value.Domains).ToDictionary(d => d.Name, d => d);

        public IEnumerable<ModelFile> Files => _modelFiles.Values;

        public IDisposable? LoadFromConfig(bool watch = false)
        {
            foreach (var mw in _modelWatchers)
            {
                var sameGeneratorList = _modelWatchers.Where(m => m.Name == mw.Name).ToList();
                mw.Number = sameGeneratorList.IndexOf(mw) + 1;
            }

            _logger.LogInformation($"Watchers enregistrés : {string.Join(", ", _modelWatchers.Select(mw => mw.FullName))}");

            FileSystemWatcher? fsWatcher = null;
            if (watch)
            {
                _logger.LogInformation("Lancement du mode watch...");
                fsWatcher = new FileSystemWatcher(_config.ModelRoot, "*.yml");
                fsWatcher.Changed += OnFSChangedEvent;
                fsWatcher.Created += OnFSChangedEvent;
                fsWatcher.IncludeSubdirectories = true;
                fsWatcher.EnableRaisingEvents = true;
            }

            _modelFiles.Clear();
            _pendingUpdates.Clear();

            _logger.LogInformation("Chargement du modèle...");

            var files = Directory.EnumerateFiles(_config.ModelRoot, "*.yml", SearchOption.AllDirectories);

            lock (_puLock)
            {
                foreach (var file in files)
                {
                    LoadFile(file);
                }
            }

            TryApplyUpdates();

            return fsWatcher;
        }

        private IEnumerable<ModelFile> GetDependencies(ModelFile modelFile)
        {
            return modelFile.Uses
               .Select(dep =>
               {
                   if (!_modelFiles.TryGetValue(dep, out var depFile))
                   {
                       _logger.LogError($"{modelFile.Path}[6,0] - Le fichier référencé '{dep}' est introuvable.");
                       throw new ModelException($"Erreur lors de la résolution des dépendances");
                   }

                   return depFile;
               });
        }

        private void OnFSChangedEvent(object sender, FileSystemEventArgs e)
        {
            _fsCache.Set(e.FullPath, e, new MemoryCacheEntryOptions()
                .AddExpirationToken(new CancellationChangeToken(new CancellationTokenSource(TimeSpan.FromMilliseconds(500)).Token))
                .RegisterPostEvictionCallback((k, v, r, a) =>
                {
                    if (r != EvictionReason.TokenExpired)
                    {
                        return;
                    }

                    OnModelFileChange((string)k);
                }));
        }

        private void OnModelFileChange(string filePath)
        {
            _logger.LogInformation(string.Empty);
            _logger.LogInformation($"Modifié: {filePath.ToRelative()}");

            lock (_puLock)
            {
                LoadFile(filePath);
            }

            TryApplyUpdates();
        }

        private void LoadFile(string filePath)
        {
            try
            {
                var file = _modelFileLoader.LoadModelFile(filePath);
                _modelFiles[file.Name] = file;
                _pendingUpdates.Add(file.Name);
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
        }

        private void TryApplyUpdates()
        {
            if (!_pendingUpdates.Any())
            {
                return;
            }

            lock (_puLock)
            {
                try
                {
                    var relationshipErrors = new List<string>();

                    var affectedFiles = _pendingUpdates.Select(pu => _modelFiles[pu]).Any(mf => mf.Domains.Any())
                        ? _modelFiles.Values
                        : _modelFiles.Values.Where(f => _pendingUpdates.Contains(f.Name)).SelectMany(pf => _modelFiles.Values.Where(f => f.Name.Equals(pf.Name) || f.Uses.Any(d => d.Equals(pf.Name)))).Distinct();

                    var sortedFiles = ModelUtils.Sort(affectedFiles, f => GetDependencies(f).Where(d => affectedFiles.Any(af => af.Name.Equals(d.Name))));

                    foreach (var affectedFile in sortedFiles)
                    {
                        relationshipErrors.AddRange(ResolveRelationshipsAndAliases(affectedFile));
                    }

                    if (relationshipErrors.Any())
                    {
                        foreach (var error in relationshipErrors)
                        {
                            _logger.LogError(error);
                        }

                        throw new ModelException("Erreur lors de la lecture du modèle.");
                    }

                    foreach (var modelWatcher in _modelWatchers)
                    {
                        modelWatcher.OnFilesChanged(sortedFiles);
                    }

                    _logger.LogInformation($"Mise à jour terminée avec succès.");

                    _pendingUpdates.Clear();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, e.Message);
                }
            }
        }

        private IEnumerable<string> ResolveRelationshipsAndAliases(ModelFile modelFile)
        {
            var dependencies = GetDependencies(modelFile).ToList();

            var referencedClasses = dependencies
                .SelectMany(m => m.Classes)
                .Concat(modelFile.Classes)
                .ToDictionary(c => c.Name, c => c);

            foreach (var value in modelFile.Relationships)
            {
                switch (value)
                {
                    case (Class classe, ClassRelation relation):
                        if (!referencedClasses.TryGetValue(relation.ReferenceName, out var extends))
                        {
                            yield return $"{modelFile.Path}{relation.Position} - La classe '{relation.ReferenceName}' est introuvable dans le fichier ou l'un de ses dépendances. ({modelFile}/{classe})";
                            break;
                        }

                        classe.Extends = extends;
                        break;
                    case (RegularProperty rp, DomainRelation relation):
                        if (!Domains.TryGetValue(relation.ReferenceName, out var domain))
                        {
                            yield return $"{modelFile.Path}{relation.Position} - Le domaine '{relation.ReferenceName}' est introuvable. ({modelFile}/{rp.Class?.Name ?? rp.Endpoint?.Name}/{rp.Name})";
                            break;
                        }

                        rp.Domain = domain;
                        break;
                    case (AssociationProperty ap, ClassRelation relation):
                        if (!referencedClasses.TryGetValue(relation.ReferenceName, out var association))
                        {
                            yield return $"{modelFile.Path}{relation.Position} - La classe '{relation.ReferenceName}' est introuvable dans le fichier ou l'une de ses dépendances. ({modelFile}/{ap.Class?.Name ?? ap.Endpoint?.Name}/{{association}})";
                            break;
                        }

                        if (association.PrimaryKey == null)
                        {
                            yield return $"{modelFile.Path}{relation.Position} - La classe '{relation.ReferenceName}' doit avoir une clé primaire pour être référencée dans une association. ({modelFile}/{ap.Class?.Name ?? ap.Endpoint?.Name}/{{association}})";
                            break;
                        }

                        ap.Association = association;
                        break;
                    case (CompositionProperty cp, ClassRelation relation):
                        if (!referencedClasses.TryGetValue(relation.ReferenceName, out var composition))
                        {
                            yield return $"{modelFile.Path}{relation.Position} - La classe '{relation.ReferenceName}' est introuvable dans le fichier ou l'une de ses dépendances. ({modelFile}/{cp.Class?.Name ?? cp.Endpoint?.Name}/{{composition}})";
                            break;
                        }

                        cp.Composition = composition;
                        break;
                    case (CompositionProperty cp, DomainRelation relation):
                        if (!Domains.TryGetValue(relation.ReferenceName, out var domainKind))
                        {
                            yield return $"{modelFile.Path}{relation.Position} - Le domaine '{relation.ReferenceName}' est introuvable. ({modelFile}/{cp.Class?.Name ?? cp.Endpoint?.Name}/{cp.Name})";
                            break;
                        }

                        cp.DomainKind = domainKind;
                        break;
                    case (AliasProperty alp, DomainRelation relation):
                        if (!Domains.TryGetValue(relation.ReferenceName, out var listDomain))
                        {
                            yield return $"{modelFile.Path}{relation.Position} - Le domaine '{relation.ReferenceName}' est introuvable. ({modelFile}/{alp.Class?.Name ?? alp.Endpoint?.Name}/{alp.Name})";
                            break;
                        }

                        alp.ListDomain = listDomain;
                        break;
                }
            }

            foreach (var value in modelFile.Relationships)
            {
                if (value is (AliasProperty alp, AliasRelation relation))
                {
                    if (!referencedClasses.TryGetValue(relation.ReferenceName, out var aliasedClass))
                    {
                        yield return $"{modelFile.Path}{relation.Position} - La classe '{relation.ReferenceName}' est introuvable dans le fichier ou l'une de ses dépendances. ({modelFile}/{alp.Class?.Name ?? alp.Endpoint?.Name}/{{alias}})";
                        break;
                    }

                    var shouldBreak = false;
                    foreach (var property in relation.IncludeReferences.Concat(relation.ExcludeReferences))
                    {
                        var aliasedProperty = aliasedClass.Properties.SingleOrDefault(p => p.Name == property.Value);
                        if (aliasedProperty == null)
                        {
                            yield return $"{modelFile.Path}[{property.Start.Line},{property.Start.Column}] - La propriété '{property.Value}' est introuvable sur la classe '{aliasedClass}'. ({modelFile}/{alp.Class?.Name ?? alp.Endpoint?.Name}/{{alias}})";
                            shouldBreak = true;
                        }
                    }

                    if (shouldBreak)
                    {
                        break;
                    }

                    var propertiesToAlias =
                        (relation.IncludeReferences.Any()
                            ? relation.IncludeReferences.Select(p => aliasedClass.Properties.Single(prop => prop.Name == p.Value))
                            : aliasedClass.Properties.Where(prop => !relation.ExcludeReferences.Select(p => p.Value).Contains(prop.Name)))
                        .Reverse();

                    foreach (var property in propertiesToAlias)
                    {
                        var prop = alp.Clone((IFieldProperty)property);
                        if (alp.Class != null)
                        {
                            alp.Class.Properties.Insert(alp.Class.Properties.IndexOf(alp) + 1, prop);
                        }
                        else if (alp.Endpoint?.Returns == alp)
                        {
                            alp.Endpoint.Returns = prop;
                        }
                        else if (alp.Endpoint?.Params.Contains(alp) ?? false)
                        {
                            alp.Endpoint.Params.Insert(alp.Endpoint.Params.IndexOf(alp) + 1, prop);
                        }
                    }

                    if (alp.Class != null)
                    {
                        alp.Class.Properties.Remove(alp);
                    }
                    else if (alp.Endpoint?.Params.Contains(alp) ?? false)
                    {
                        alp.Endpoint.Params.Remove(alp);
                    }
                }
            }

            foreach (var alias in modelFile.Aliases)
            {
                var referencedFile = dependencies.SingleOrDefault(dep => dep.Name == alias.File);
                if (referencedFile == null)
                {
                    yield return $"{modelFile.Path} - Le fichier '{alias.File}' est introuvable dans les dépendances du fichier. ({modelFile}/{{alias}})";
                    break;
                }

                foreach (var className in alias.Classes)
                {
                    var referencedClass = referencedFile.Classes.SingleOrDefault(classe => classe.Name == className);
                    if (referencedClass == null)
                    {
                        yield return $"{modelFile.Path} - La classe '{className}' est introuvable dans le fichier '{alias.File}'. ({modelFile}/{{alias}})";
                        break;
                    }

                    if (!modelFile.Classes.Any(classe => classe.Name == referencedClass.Name))
                    {
                        modelFile.Classes.Add(referencedClass);
                    }
                }

                foreach (var endpointName in alias.Endpoints)
                {
                    var referencedEndpoint = referencedFile.Endpoints.SingleOrDefault(endpoint => endpoint.Name == endpointName);
                    if (referencedEndpoint == null)
                    {
                        yield return $"{modelFile.Path} - L'endpoint '{endpointName}' est introuvable dans le fichier '{alias.File}'. ({modelFile}/{{alias}})";
                        break;
                    }

                    if (!modelFile.Endpoints.Any(endpoint => endpoint.Name == referencedEndpoint.Name))
                    {
                        modelFile.Endpoints.Add(referencedEndpoint);
                    }
                }
            }

            if (!_config.AllowCompositePrimaryKey)
            {
                foreach (var classe in modelFile.Classes)
                {
                    if (classe.Properties.Count(p => p.PrimaryKey) > 1)
                    {
                        throw new ModelException($"La classe {classe.Name} du fichier {modelFile} doit avoir une seule clé primaire ({string.Join(", ", classe.Properties.Where(p => p.PrimaryKey).Select(p => p.Name))} trouvées)");
                    }
                }
            }
        }
    }
}