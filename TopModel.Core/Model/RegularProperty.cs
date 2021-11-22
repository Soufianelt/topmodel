﻿namespace TopModel.Core;

public class RegularProperty : IFieldProperty
{
#nullable disable
    public string Name { get; set; }
#nullable enable

    public string? Label { get; set; }

    public bool PrimaryKey { get; set; }

    public bool Required { get; set; }

#nullable disable
    public Domain Domain { get; set; }

    public string Comment { get; set; }

    public Class Class { get; set; }

    public Endpoint Endpoint { get; set; }
#nullable enable

    public string? DefaultValue { get; set; }
}