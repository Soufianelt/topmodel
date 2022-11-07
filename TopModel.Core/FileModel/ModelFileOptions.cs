﻿#nullable disable
#pragma warning disable SA1402

namespace TopModel.Core.FileModel;

public class ModelFileOptions
{
    public EndpointOptions Endpoints { get; set; } = new();
}

public class EndpointOptions
{
#nullable enable
    public string? FileName { get; set; }

    public string? Prefix { get; set; }
}